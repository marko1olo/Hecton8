# HECTON-8 Quality Gates Bible

Status: AUTHORING STANDARD - PENDING UNITY/PROFILER VERIFICATION
Scope: cross-system acceptance gates, proof artifacts, screenshot review, profiler proof, taste review, low-tier validation, and anti-fake reporting.

## 0. Prime Quality Law

No system is accepted because it sounds good in chat.

Acceptance requires proof appropriate to the change:

- static scan;
- screenshot;
- render capture;
- Unity Profiler;
- GCMonitor;
- Frame Debugger;
- Memory Profiler;
- generated asset manifest;
- gameplay repro;
- black-box dump;
- low-tier capture.

If proof is missing, status is `PENDING VERIFICATION`.

## 1. Universal Review Questions

Every player-facing change must answer:

- What physical fact does this reveal?
- What player decision does this sharpen?
- What fails?
- What remains readable on compact hardware?
- What does high-end add without changing truth?
- What cheaper fake was considered?
- What artifact proves the claim?

If the answer is only "looks cool", reject.

## 2. Screenshot Gate

Every visual/UI/world/asset change needs screenshots when implemented:

- normal tier;
- compact/low tier;
- target aspect ratio;
- 720p or low render scale;
- debug view if relevant: wireframe, colliders, masks, LODs, overdraw, or UI layout.

Reject beauty shots that hide problems.

## 3. Performance Gate

Runtime claims require:

- before/after frame time;
- GC allocation;
- memory/VRAM delta if assets or RTs changed;
- load-shed path;
- exact scene/repro;
- hardware/tier statement.

Any single feature over 0.1 ms is suspicious until proven.

## 4. Taste Gate

Taste proof requires checking against:

- `taste.md`;
- relevant system bible;
- low-tier readability;
- screenshot consequence;
- Deep Sea Noir/NASA-punk identity;
- anti-generic rejection list.

If the work could belong unchanged in a generic sci-fi survival game, reject.

## 5. Generated Asset Gate

Generated assets must prove:

- mesh validation;
- UV/texture validation;
- material route;
- LOD chain;
- collision proxy;
- render proof;
- low-tier proof;
- manifest path.

No generated asset enters production because it merely exists.

## 6. UI Gate

UI must prove:

- task clarity in 3 seconds;
- no text clipping;
- localization expansion;
- color roles;
- input navigation;
- zero-GC hot path;
- low-tier screenshot;
- no decorative-only graphics.

## 7. Gameplay Gate

Gameplay must prove:

- player decision;
- physical consequence;
- failure evidence;
- resource/resource-free justification;
- save/authority ownership;
- no hidden hot polling;
- no binary quality switch.

## 8. Audio Gate

Audio must prove:

- information carried;
- priority/mix behavior;
- no spam;
- low-tier path;
- no hot-path allocations;
- cue IDs not runtime strings.

## 9. Final Report Shape

A valid report says:

- what was wrong;
- what changed;
- in-game result;
- what was verified;
- what remains `PENDING VERIFICATION`;
- exact files;
- exact proof artifact or explicit absence.

Reports that claim quality without evidence are rejected.
