# HECTON-8 Inventory, Resources, Crafting, And Economy Bible

Status: AUTHORING LAW / STATIC DOC / RUNTIME PROOF NOT IMPLIED
Scope: items, resource identity, inventory data, storage, crafting, salvage economy, repair costs, extraction pressure, logistics links, and proof gates.

## Prime Law

Inventory is physical logistics under pressure, not abstract loot collection.

Every item must imply material, origin, condition, use, volume, risk, and extraction cost. HECTON-8 rejects colored-rock currency, infinite pockets, generic crafting ladders, clean resource icons, and progression that ignores oxygen, pressure, route, power, seals, pumps, and survival debt.

## Truth Ownership

Inventory/economy truth is split:

- `data.md` owns item ids, DTO layout, static database, and binary payloads;
- `inventory.md` owns item meaning, storage rules, economy pressure, and crafting acceptance;
- `construction.md` owns base/buildable use;
- `persistence.md` owns save identity and world scars;
- `tools.md` owns harvesting/cutting/scanning interactions;
- UI presents inventory but does not own item truth.

## Item And Resource Identity

Resources must be physically credible:

- seal kit, pressure glass, cable spool, filter media, pump casing, valve core, relay board, ceramic insulator, oxygen cartridge, salvage plate, hull laminate, biological sample, contaminated tissue, black-box shard.

Rules:

- item name indicates function or origin;
- icon/model matches material;
- condition/wear matters where useful;
- rarity follows world logic, not color tier fantasy;
- stack rules follow mass/volume/fragility;
- salvage leaves a visible scar or state change when relevant.

## Inventory Runtime Law

Required:

- item ids are numeric/stable;
- runtime inventory uses SOA/native-friendly layout;
- no string lookup in hot inventory queries;
- recipe validation uses ids, bitsets, hashes, or baked tables;
- storage containers have identity, capacity, filters, and persistence;
- dropped proxies are dumb views of data records;
- UI reads snapshots and formats text allocation-free.

## Crafting And Economy

Crafting creates decisions:

- repair now or expand later;
- power scanner or oxygen system;
- strip evidence or preserve truth;
- carry heavy salvage or move faster;
- build comfort only after survival infrastructure;
- burn rare seals to stay alive or save them for depth progression.

Recipes must be validated against material reality. A pump needs casing, seals, power path, and intake/output logic. A pressure door needs frame, gasket, actuator, and lock state. Magic conversion is rejected.

## GlobalQualityWeight Scaling

`GlobalQualityWeight` may scale visual inventory richness, icon detail, item proxy density, storage animation, UI diagnostics, and noncritical presentation. It must not change item ids, recipe truth, stack rules, save identity, or authority route.

Compact keeps clear item silhouettes, numeric ids, stable UI, simple proxies, and readable scarcity. High tiers add richer item models, dirt, labels, damage state, and diegetic storage feedback.

## Proof Artifacts

Inventory/economy work must provide:

- item id/schema list;
- recipe/source table;
- storage capacity rules;
- save/load proof if persistent;
- UI snapshot proof;
- compact-tier readability capture;
- no string/hot allocation route if implemented;
- resource use/failure decision statement.

## Rejection Gates

Reject:

- abstract currency as core progression;
- resources with no physical use;
- infinite inventory without explicit mode reason;
- recipes that ignore world/material logic;
- item ids derived from names at runtime;
- inventory UI owning item truth;
- economy reports without save/data proof.

## Acceptance Sentence

Inventory and economy are accepted only when items are physical, ids are stable, crafting creates survival choices, storage persists correctly, compact UI remains readable, and runtime claims have proof.
