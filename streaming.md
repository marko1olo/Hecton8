# HECTON-8 Streaming And Residency Bible

Status: AUTHORING LAW / STATIC DOC / RUNTIME PROOF NOT IMPLIED
Scope: Addressables, asset lifecycle, memory residency, HLOD, biome streaming, release order, VRAM pressure, and load proof.

## Prime Law

The ocean must feel vast without hiding stalls. Runtime asset access routes through owned async loading, residency ledgers, priority queues, and explicit release. HECTON-8 rejects synchronous load spikes, orphaned handles, material duplication, and memory pressure discovered only after a crash.

## Asset Access Route

Runtime asset requests use Addressables or the approved project asset route. Every loaded handle must have:

- stable asset key;
- owner;
- ref count;
- priority tier;
- last access tick;
- estimated RAM/VRAM size;
- release ledger entry.

No orphaned handle is allowed. Ref count below zero is a defect. Asset presence in memory is not proof of GPU residency.

## Truth Ownership

Streaming owns residency, load admission, release order, memory pressure, and lifecycle evidence. It does not own gameplay truth, save identity, world generation truth, UI state, or collision truth.

Gameplay systems request assets through stable keys and owner routes. Streaming may deny, delay, downgrade, or unload presentation content according to priority, but it must not remove player-critical truth or return-path readability.

## Priority Tiers

Loading priority must reflect player survival and readability:

- Tier 0: player suit, HUD, scanner, breath/life support, failure UI.
- Tier 1: held tools, active vehicle/cockpit, currently interacted machines.
- Tier 2: immediate world chunks, collision proxies, near hazards.
- Tier 3: active ambience, reverb, warning banks.
- Tier 4: mid-range fauna, props, VFX support.
- Tier 5: distant HLOD, far silhouettes, biome dressing.
- Tier 6: speculative preload.

Under pressure, distant and speculative work is cut first. Player survival assets never compete with decorative dressing.

## World Streaming

World streaming must consider:

- player position and velocity;
- depth band;
- route intent;
- biome gate;
- current pressure;
- active mission/salvage targets;
- return path safety;
- expected load budget.

Prefetch is predictive but bounded. The stream system must not create a beautiful distant biome by starving near-field tools, UI, physics, or collision.

## HLOD And Generated Assets

Generated assets must arrive with LOD chains and HLOD merge policy from `PROCEDURAL_ASSET_PIPELINE.md`. Streaming cannot fix missing LODs at runtime.

HLOD rules:

- far clusters use merged meshes, impostors, or proxy silhouettes;
- material slots stay shared;
- collision is disabled or proxy-only beyond interaction radius;
- near upgrade is async and hysteresis-controlled;
- visual transition must hide load cadence, not stall gameplay.

## Release Order

Release must be ordered:

1. unregister from tick/update/phase dispatch;
2. unsubscribe signals;
3. disable renderers/audio/particles;
4. return pooled objects or destroy approved non-pooled objects;
5. release Addressables handle;
6. remove registry record;
7. update residency and pressure ledgers.

Releasing handles before disabling/returning runtime objects is rejected.

## Memory Pressure

Memory pressure response is continuous. It may reduce mip residency, LOD distance, speculative slots, audio bank density, VFX pools, and texture quality. It must not remove gameplay truth, break UI readability, or unload the return route while the player depends on it.

## GlobalQualityWeight Scaling

`GlobalQualityWeight` may scale prefetch distance, speculative load slots, HLOD residency radius, mip bias, decorative biome density, audio bank breadth, VFX support residency, and diagnostic ledger depth. It must not change save identity, gameplay truth, asset ownership, release order, collision truth, or required near-field survival assets.

Compact keeps player survival assets, UI, held tools, collision proxies, and return-route silhouettes resident before decorative content. Middle expands near biome dressing and audio/VFX support. High increases HLOD continuity and texture residency. Ultra keeps richer far silhouettes and biome dressing only while memory pressure proof remains green.

## Rejection Gates

Reject:

- synchronous loads in gameplay;
- runtime-generated fallback meshes because streaming missed an asset;
- missing release ledger;
- asset groups with no memory evidence;
- material-per-instance assets entering stream pools;
- HLOD with wrong silhouette or missing route cues;
- reports without loaded handle count, resident memory, VRAM estimate, and pressure behavior.

## Proof Artifacts

Streaming work must provide:

- loaded handle count and owner list;
- release ledger sample;
- RAM and VRAM estimate;
- priority queue state;
- load/release timing;
- HLOD transition capture where visual;
- Compact memory-pressure behavior;
- profiler/GC proof if runtime streaming code changed;
- black-box fields for missing handle, failed load, stale reference, and pressure-triggered downgrade.

## Acceptance Sentence

Streaming is accepted only when the world remains readable, near-field gameplay never starves, memory pressure is measured, and large-scale beauty is delivered through planned residency instead of runtime panic.
