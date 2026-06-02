# HECTON-8 Input, Rebinding, And Haptics Bible

Status: AUTHORING LAW / STATIC DOC / RUNTIME PROOF NOT IMPLIED
Scope: device abstraction, keyboard/mouse, gamepad, Steam Deck, accessibility remapping, input buffering, haptic queues, UI navigation, and input proof gates.

## Prime Law

Input is a life-support control surface, not a convenience wrapper around Unity callbacks.

The player must feel like an operator touching dangerous machinery through gloves, suit servos, cockpit controls, terminals, and worn emergency hardware. Input must be predictable under stress, readable across devices, and allocation-free in runtime hot paths. If a control scheme works only because the player adapts to delay, missing buffers, noisy haptics, or device-specific quirks, reject it.

## Truth Ownership

Input owns normalized device state, action bitmasks, buffered action windows, device identity, control scheme state, and haptic command dispatch. It does not own movement, tools, UI state, vehicles, combat, save state, construction, or survival truth.

Runtime consumers read immutable input snapshots from the owning input phase. They must not poll Unity input APIs directly, subscribe gameplay lambdas, search action maps by string, or query devices as a side effect of gameplay logic.

## Runtime Contract

Required:

- one input polling boundary per frame;
- cached action references after boot or action-map change;
- `PlayerInputState` or equivalent blittable snapshot with normalized axes and bitmask buttons;
- bounded input buffer for short timing windows;
- fixed-capacity haptic command queue;
- deterministic device classification;
- no gameplay truth in UI event callbacks;
- explicit ownership for action consumption order.

Forbidden:

- `Input.GetKey`, `Input.GetAxis`, `Input.GetButton` in gameplay paths;
- per-frame `Gamepad.current` discovery;
- runtime string-keyed action lookup;
- lambda subscriptions for gameplay input hot paths;
- unbounded `List<>` or event fanout for input actions;
- haptic rumble that does not map to a named physical cause.

## Rebinding And Settings

Rebinding is a settings/data operation, not gameplay logic.

Rules:

- rebind maps are loaded during boot or menu-safe phases;
- runtime hot paths read action ids, not action names;
- duplicate bindings must be detected before save;
- inaccessible bindings must be rejected before confirmation;
- gamepad, keyboard/mouse, Steam Deck, and accessibility routes must share the same action semantics;
- UI navigation has a separate action layer from gameplay, with explicit focus ownership.

## Haptics

Haptics must report physical state:

- low-frequency: hull impact, pressure, seismic pulse, heavy machinery, vehicle mass;
- high-frequency: cutter, drill, sonar, scanner, electrical fault, small tool contact;
- trigger resistance: brake, squeeze, tool spin-up, docking latch, pressure lock;
- ambient pulse: rare, priority-limited, and tied to threat, oxygen, or hull stress.

Every haptic command has cause, priority, duration, motor mask, decay, and owner. Ambient haptics must yield to critical events. No constant decorative rumble.

## GlobalQualityWeight Scaling

`GlobalQualityWeight` may scale haptic layering, UI navigation previews, analog smoothing depth, gyro/filter richness, device diagnostics, and optional accessibility visualization. It must not change action semantics, action ids, buffer ownership, save layout, or gameplay authority.

Compact keeps clean snapshots, buffered actions, core haptics, and readable prompts. Middle adds richer haptic priority blending. High adds adaptive trigger and device-specific polish. Ultra adds deeper sensory layering only when comfort and clarity remain intact.

## Proof Artifacts

Input work must provide:

- action map list and action id map;
- device matrix: keyboard/mouse, gamepad, Steam Deck if claimed;
- hot-path static scan for forbidden input APIs;
- zero-GC proof for gameplay input path when implemented;
- rebinding duplicate/conflict test;
- UI navigation proof;
- haptic priority map;
- compact-tier control readability proof.

## Rejection Gates

Reject:

- gameplay code that polls Unity input directly;
- device-specific behavior without abstraction;
- haptics that feel random or constant;
- remapping that can produce unreachable actions;
- UI navigation that requires mouse only;
- action names used in hot runtime queries;
- input reports that claim zero-GC without profiler or GCMonitor proof.

## Acceptance Sentence

Input is accepted only when a single owner publishes allocation-free normalized state, all consumers read snapshots, rebinding is safe, haptics carry physical meaning, compact devices remain playable, and runtime claims have proof.
