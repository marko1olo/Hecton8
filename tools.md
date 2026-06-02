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

## 8. GlobalQualityWeight Scaling

Compact keeps target clarity, correct verb, sound, haptic priority, simple material response, and bounded queries. Middle adds richer sparks, heat, decals, and scan visuals. High adds better target material response and failure feedback. Ultra adds secondary smoke, slag, tool body motion, and richer cockpit/visor integration without changing the interaction result.

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
