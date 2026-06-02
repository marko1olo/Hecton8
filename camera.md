# HECTON-8 Camera, View, And Capture Bible

Status: AUTHORING LAW / STATIC DOC / RUNTIME PROOF NOT IMPLIED
Scope: player camera, cockpit camera, tool camera response, camera shake, cutscenes, screenshot/trailer capture, FOV, comfort, culling visibility, and camera proof gates.

## Prime Law

Camera is an instrument under pressure. It is not a floating drone, not a screenshot machine during gameplay, and not a place to hide weak assets.

The camera must preserve route readability, tool affordances, threat scale, oxygen/pressure stress, and physical embodiment. A beautiful frame that hides the next decision is a failed camera. Constant shake, generic sway, fake cinematic drift, and over-framed threats are rejected.

## Truth Ownership

Camera owns view transform presentation, framing, FOV, shake response, cut transitions, capture rigs, and comfort filters. It does not own player movement, input, vehicle velocity, threat state, UI truth, damage, or route state.

Camera consumes published state from `player.md`, `vehicles.md`, `tools.md`, `survival.md`, `ai.md`, `physics.md`, and `ui.md`. It must not discover scene truth through searches or physics queries in hot paths.

## Gameplay View Rules

Required:

- preserve interaction target visibility;
- preserve one readable route cue under fog/darkness;
- keep tool contact points visible during operation;
- avoid clipping through helmet/cockpit/world geometry;
- keep FOV stable enough for comfort;
- expose pressure/impact through short event response, not permanent noise;
- separate gameplay camera from capture-only rigs.

Camera shake is event-based, priority-limited, duration-capped, and load-sheddable. Hull impacts, sonar shocks, decompression, large fauna near-misses, docking, tool kick, and seismic pulses may shake. Ambient shake without physical cause is noise.

## Cockpit And Vehicle Camera

Vehicle view must communicate mass:

- acceleration has delayed body response;
- cockpit camera inherits platform frame correctly;
- docking preserves spatial continuity;
- ballast/pitch/roll changes have readable motion;
- instrument panels stay legible during stress;
- emergency camera effects never hide fatal warnings.

Boarding, docking, EVA handoff, and terminal transitions must feel like moving through physical equipment. Teleport-like cuts are allowed only for debug, loading, black-box replay, or explicit scene transitions.

## Capture And Trailer Rigs

Capture cameras are allowed for marketing, screenshots, trailers, and QA, but they must not invent gameplay truth.

Rules:

- capture scene must state whether it is gameplay, staged in-engine, editor render, or concept;
- no screenshots that hide missing route/function behind fog;
- no trailer shots implying mechanics not implemented;
- capture rigs must be excluded from runtime gameplay control unless explicitly owned;
- public captures must pass `textes.md`, `presentation.md`, and `quality.md`.

## GlobalQualityWeight Scaling

`GlobalQualityWeight` may scale secondary motion, procedural shake sample count, camera material effects, view dirt, depth-of-field, capture render scale, and cinematic polish. It must not change camera truth ownership, interaction visibility, input response, or motion comfort.

Compact keeps stable view, route readability, minimal shake, and no expensive camera post. Middle adds stronger event response and subtle cockpit motion. High adds richer lens dirt, visor glass, physical micro-motion, and capture polish. Ultra adds cinematic response only after gameplay readability remains intact.

## Production Packet

Any camera, cockpit camera, shake, view, capture, or trailer-rig change must declare:

- camera owner and active state route;
- input and interaction visibility constraints;
- shake cause, amplitude, decay, and comfort limits;
- vehicle/cockpit attachment and platform-relative behavior if relevant;
- capture truth label for screenshots or footage;
- Compact and High capture proof;
- accessibility/motion reduction behavior;
- profiler/GC/GPU proof when runtime camera or capture code changes.

Camera presentation must make route, danger, and instrument state clearer. Motion that only looks expensive is rejected.

## Proof Artifacts

Camera work must provide:

- gameplay view capture;
- compact-tier view capture;
- interaction target visibility proof;
- route cue proof under representative fog/darkness;
- shake cause/duration/priority list if shake changed;
- comfort note for FOV/motion;
- capture rig truth label for screenshots/trailers;
- profiler proof if runtime post/camera systems changed.

## Rejection Gates

Reject:

- constant shake;
- camera hiding controls, route, or interaction targets;
- beauty shots with no decision/evidence;
- cockpit camera that ignores vehicle motion;
- capture shots implying unbuilt mechanics;
- camera scripts that own gameplay truth;
- runtime camera effects without load-shed path.

## Acceptance Sentence

Camera is accepted only when it preserves decisions, sells mass and pressure, remains comfortable, labels capture truth, scales continuously, and never hides weak systems behind cinematic motion.
