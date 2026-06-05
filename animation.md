# HECTON-8 Animation Bible

Status: AUTHORING LAW / STATIC DOC / RUNTIME PROOF NOT IMPLIED
Evidence class: STATIC_DOC
Scope: player motion, creature motion, IK, tools, rigs, procedural animation, VAT, secondary motion, and animation proof.

## First-20 Route Hook

- First-20 moment: first exit, swim, tool interaction, and first hazard response need heavy equipment motion, readable tool phases, contact confidence, and creature/threat intent.
- Route blocker removed: prevents opening-route animation work from shipping floaty locomotion, unclear tool contact, or creature motion that gives no survival read.
- Proof class: STATIC_DOC only; route acceptance still requires in-engine capture, compact readability proof, and profiler/GC evidence when runtime animation changes.

## Prime Law

Animation must sell mass, pressure, fatigue, equipment weight, and threat intent. HECTON-8 rejects floaty humanoids, toy creatures, canned loops with no contact truth, and procedural motion that looks like mathematical noise.

## Rig And Clip Contract

Every animated asset must define:

- scale in meters;
- skeleton root and forward axis;
- contact points;
- center-of-mass reference;
- tool sockets;
- damage or state masks;
- LOD animation strategy;
- whether the asset uses skeletal animation, VAT, shader sway, or static pose swaps.

Animation clips are source material, not runtime authority. Runtime systems consume normalized state, stable handles, and preallocated buffers.

## Truth Ownership

Animation owns presentation of pose, timing, blend, contact correction, and secondary motion. It does not own player input truth, tool hit truth, creature cognition truth, vehicle motion truth, or physics collision truth.

Gameplay systems publish state and intent. Animation consumes that state and returns only approved presentation outputs or explicit contact confidence records where the owning system asks for them.

## IK Law

IK runs in the animation phase through batched jobs or PlayableGraph jobs. It does not run as per-object `LateUpdate` hacks.

Required:

- cached bone indices;
- preallocated chain buffers;
- async raycasts consumed frame N+1 when surface contact is needed;
- analytical two-bone solvers for arms/legs;
- FABRIK or CCD only for multi-joint chains;
- clamp to prevent hyperextension;
- temporal smoothing for contact transitions;
- fallback to authored pose when target confidence is low.

Forbidden:

- per-bone Transform traversal in hot evaluation;
- `GetComponent` in IK;
- per-character raycast storms;
- same-frame raycast dependency for every foot or hand;
- IK that breaks silhouette readability.

## Creature Motion

Creature motion must expose intent before full reveal. Movement is judged by behavior readability, not by number of bones.

Required for major fauna:

- idle pressure behavior;
- search behavior;
- threat telegraph;
- attack or avoidance intent;
- damaged or repelled state;
- sound/light/sonar response;
- silhouette preservation across animation LOD.

Tentacles, tails, fins, feelers, and membranes use authored clips plus bounded procedural offsets. Ultra may add richer secondary motion, but low tier must retain the same threat read.

## Player And Tool Animation

Player animation must preserve input clarity. Camera sway, hand motion, tool recoil, ladder or hatch motion, and vehicle cockpit response must never hide the next physical decision.

Tools need operation phases:

- ready;
- acquire target;
- contact;
- commit;
- fail;
- cool down;
- stow.

Each phase must have audio/haptic/UI presentation hooks, but gameplay truth remains owned by the tool system.

## VAT And Shader Animation

Use VAT, vertex color masks, and shader deformation for repeated non-interactive motion: flora sway, soft fins, distant creature loops, hanging cables, loose straps, and debris vibration.

Runtime CPU animation is reserved for player-critical bodies, nearby creatures, and interaction-critical tools. Background visual motion belongs on the GPU or in baked clips.

## Scalability

`GlobalQualityWeight` scales IK pass density, secondary motion layers, blend polish, VAT detail, update cadence for non-authority pose polish, and optional contact refinement. It never changes input truth, hit truth, physics authority, vehicle motion truth, or animation phase ownership.

Compact uses authored clips, limited IK, VAT, lower update cadence, and strong silhouettes. Middle adds more contact correction. High adds richer secondary motion and better blending. Ultra adds denser procedural detail after animation phase budgets are proven.

## Proof Artifacts

Animation work must provide:

- rig/clip/VAT route;
- update phase and owner;
- contact point list and confidence route;
- IK chain count, raycast count, and fallback behavior;
- LOD animation strategy;
- Compact and High capture for player-facing motion;
- profiler/GC proof when runtime animation code changed;
- black-box or telemetry fields for critical player/creature/vehicle motion faults.

Foot sliding, contact misses, and deformation errors remain `PENDING VERIFICATION` until captured in-engine.

## Rejection Gates

Reject:

- foot sliding on close characters;
- tool motion with no contact truth;
- creature loops with no intent;
- IK inside unmanaged object scripts;
- per-frame allocations;
- animation that changes gameplay truth from presentation;
- low-poly or toy motion hidden under camera shake.

## Acceptance Sentence

Animation is accepted only when it makes equipment heavy, creatures intentional, tools tactile, and the ocean physically oppressive without violating phase, budget, or zero-GC laws.
