# HECTON-8 Tools And Interaction Bible

Status: AUTHORING STANDARD - PENDING UNITY/PROFILER VERIFICATION
Scope: tools, equipment, raycast interaction, repair, welding, cutting, scanning, drilling, tethering, heat, power, physical affordances, and interaction feedback.

## 0. Prime Tool Law

Tools are verbs made physical.

Every tool must change a route, reveal evidence, repair a system, create risk, or alter a physical state. A tool that only plays an animation and increments a progress bar is weak.

## 1. Tool Identity

Every tool must declare:

- verb;
- target classes;
- range;
- power/heat/noise/oxygen/cooldown cost;
- failure state;
- physical feedback;
- UI/readout carrier;
- world consequence.

The player should understand what the tool is doing from sound, haptics, VFX, target material response, and UI state.

## 2. Interaction Targets

Targets must communicate affordance:

- cut line;
- weld seam;
- latch;
- valve;
- panel screws;
- pressure seal;
- damaged pipe;
- scanner tag;
- salvage grip;
- black-box port.

Do not make the player pixel-hunt generic colliders. The world mesh, decals, labels, lighting, and tool cursor must agree.

## 3. Feedback Stack

Tool feedback must include the right subset:

- reticle/target lock;
- physical target highlight or decal;
- sound;
- haptic;
- heat/power readout;
- material response;
- pooled VFX;
- failure reason.

Progress bars are allowed only when paired with physical progress: seam fill, cut depth, pump pressure, scan confidence, heat, or lock state.

## 4. Runtime Discipline

Tools must obey:

- no per-frame allocation;
- bounded ray/query buffers;
- no concrete tool-to-tool coupling;
- numeric IDs and capability masks;
- interactions routed through owners/signals;
- no direct ownership of heavy physics constraints by tool modules;
- fake cables, heat shimmer, sparks, slag, and recoil unless physical truth is required.

## 5. Upgrade Taste

Tool upgrades should change decisions:

- longer safe range;
- less noise;
- better material penetration;
- lower heat;
- clearer scan truth;
- new target class;
- faster emergency use with higher cost.

Flat numeric upgrades with no decision change are rejected.

## 6. Tool QA Gates

Reject if:

- tool has no physical consequence;
- target affordance is unclear;
- feedback is only UI;
- interaction allocates or searches scene;
- upgrade is just bigger number;
- VFX hides lack of material response;
- failure state is absent;
- the tool could belong unchanged in a generic sci-fi game.

## 7. Truth Ownership

Tool truth is owned by the interaction/tool system, not by VFX, UI, animation, or audio. The tool owner publishes target ID, verb, confidence, progress, heat, power draw, noise, failure reason, and commit result. Presentation systems consume those facts.

Interaction targets are serialized anchors, capability masks, material classes, or stable IDs. Runtime string lookup, scene search, or guessing from decorative mesh names is rejected.

## 7.1 2026-06-05 Static Source Anchors

Evidence class: STATIC_SOURCE only. Compile, Unity import, Play Mode, profiler, GC, save/load, power-grid, and player-build proof remain PENDING VERIFICATION.

| Runtime | Owner / boundary | Static route | GlobalQualityWeight consequence | Missing proof |
|---|---|---|---|---|
| `Assets/_Project/Scripts/SeafloorDrillTool.cs` | `Hecton8.Gameplay`; handheld player tool. It is a short-range `PlayerTool`/`IToolModule` with `ToolCapabilityMasks.Drill`, not a deployable mining machine. | Publishes a controlled `InteractionSignal` with `InteractionEffectType.Drill` through the cached interaction service after resolving player pose/ray. Static text says the primary action drives a short controlled bore into Drill-gated resource nodes. | No direct quality read is visible; quality may scale feedback richness only. Handheld drill success, target class, and interaction result must stay stable. | No allocation proof, ray/query budget proof, target affordance capture, interaction runtime proof, durability coupling proof, or save/world-state proof was provided by this static audit. |
| `Assets/_Project/Scripts/Gameplay/Mining/DeployableSdfDrillRuntime.cs` | `Hecton8.Gameplay.Mining`, DataVault owner `SystemID.GameplayTools`; deployable powered thumper drill. It is separate from the handheld `SeafloorDrillTool`: power-gated, placeable/poolable, inventory-bearing, acoustic-risk-producing, and SDF-aware. | Registers cold tick, late-frame tick, origin-shift, pool, cuttable, hot-swap, and interactable tree routes. Requires high power budget in static source, owns deployable inventory/black-box/extraction DataVault buffers, probes SDF/terrain through voxel read models, and emits `AcousticPingSignal`, `ItemAcquiredSignal`, `CombatDamageSignal`, and `DebrisSpawnSignal`. | Reads global quality for SDF snap/visual carve cadence with hysteresis. Static source indicates quality affects presentation cadence/weight; extraction truth, inventory, power gating, and acoustic risk must not be reduced by graphics quality. | No power-grid runtime, placement UX, SDF carve replay, item acquisition proof, threat response proof, profiler/GCMonitor, save/load, or black-box dump artifact was provided by this static audit. |
| `Assets/_Project/Scripts/Tools/ToolDurabilitySystem.cs` | `Hecton8.Tools`, DataVault owner `SystemID.GameplayTools`; centralized tool durability service and save participant. It owns durability state, not the individual tool verb or visual feedback. | Implements `ISaveable`, `ISlowTickable`, `IUpdatable`, `ILateFrameTickable`, `IToolDurabilityService`, and hot-swap listeners. Registers `GlobalRegistry.ToolDurability`, save service, player-priority tick routes, and DataVault buffers `ToolDurabilityItemStates`, `ToolDurabilityPendingDecay`, `ToolDurabilityWearMultipliers`, `ToolDurabilitySlotActive`, and `ToolDurabilityBreakdownFlags`. Publishes `ItemDurabilityChangedSignal`; reads player context and `IBrineFluidDensityReadModel` for environmental corrosion. Save/load priorities are both 20. | No direct `GlobalQualityWeight` read is visible. Quality may scale presentation of wear/corrosion only; durability truth, break flags, save identity, and item hashes must not change by graphics tier. | No save/load roundtrip, brine corrosion scenario, signal overflow telemetry, profiler/GCMonitor, Unity import, or gameplay durability proof was provided by this static audit. |

## 8. GlobalQualityWeight Scaling

Compact keeps target clarity, correct verb, sound, haptic priority, simple material response, and bounded queries. Middle adds richer sparks, heat, decals, and scan visuals. High adds better target material response and failure feedback. Ultra adds secondary smoke, slag, tool body motion, and richer cockpit/visor integration without changing the interaction result.

## First-20 Route Hook

- First-20 moment: tool and craft/repair/build must prove one scan, cut, repair, drill, or harvest action that changes route safety, capability, evidence, or resource state.
- Route blocker removed: early tools cannot be animation-only progress bars, unclear target colliders, or feedback-only verbs with no owned world result.
- Proof class: screenshot or capture of target affordance and physical feedback, Play Mode/player capture for success/failure, Profiler/GCMonitor for query/update paths, and save/load artifact when the tool changes persistent world or inventory state.

## 9. Proof Artifacts

A tool implementation must provide:

- target classes and capability mask;
- query budget and allocation proof;
- success/failure state list;
- physical feedback screenshot or capture;
- audio/haptic cues;
- power/heat/noise cost;
- low-tier readability proof;
- save/authority note if the tool changes world state.

## 10. Acceptance Sentence

A tool is accepted only when it changes a physical state or decision, has stable target truth, gives multi-channel feedback, scales without losing readability, and proves its hot path does not allocate or search the scene.
