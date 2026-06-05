# HECTON-8 Player Feel Bible

Status: AUTHORING STANDARD - PENDING UNITY/PROFILER VERIFICATION
Evidence class: STATIC_DOC
Scope: input, controls, camera feel, movement, vehicle/submarine control feel, haptics, interaction cadence, and player embodiment.

## 0. Prime Player Law

The player is a vulnerable operator inside hostile machinery, not a floating camera.

Every control system must communicate mass, water, pressure, tool handling, suit limits, and route commitment. If movement feels like a generic FPS controller with an ocean shader around it, reject it.

## 1. Input Contract

Input must be predictable and zero-GC:

- one polling boundary per frame;
- normalized input state;
- bitmask action state;
- buffered actions for short timing windows;
- no string action lookup in runtime hot paths;
- no `Input.GetKey`, `Input.GetAxis`, or callback-lambda gameplay logic;
- haptic commands use bounded priority queues.

Controls must support keyboard/mouse, gamepad, and accessibility alternatives without changing gameplay truth.

## 2. Movement Feel

Movement should feel constrained by environment:

- submerged motion has drag, inertia, and body limits;
- interiors feel close, heavy, and mechanical;
- zero-G or vehicle transitions preserve reference frames;
- tool use should affect stance, camera, haptics, and audio;
- sprint/boost costs a resource or creates risk.

Reject arcade glide if it does not serve a specific tool or vehicle fantasy.

## 3. Camera Law

Camera is an instrument:

- it must preserve route readability;
- it must not hide interaction targets;
- shake is event-based, short, and load-sheddable;
- head/visor/cockpit motion follows physical carrier logic;
- camera transitions must preserve spatial continuity unless the game intentionally cuts to a black-box or system view.

Constant shake, floaty camera drift, and cinematic framing that hides decisions are rejected.

## 4. Haptics

Haptics are physical facts:

- low-frequency for hull, pressure, collision, heavy machinery;
- high-frequency for tools, sonar, electrical faults, scanner feedback;
- priority prevents spam;
- critical haptics override ambient haptics;
- all values clamp and reject NaN/Infinity.

No decorative rumble. If the player cannot explain what the vibration means, it is noise.

## 5. Vehicles And Platforms

Vehicle/player interaction must preserve relative frames:

- player inside moving craft inherits platform motion correctly;
- cockpit controls are physical or diegetic;
- vehicle sound, camera, haptics, and UI change with load and pressure;
- docking and transitions have procedural weight;
- no teleport-feeling boarding unless explicitly a debug route.

Submarine control should feel expensive, delayed, and massive, not like a drone toy.

## 6. Player QA Gates

Reject if:

- control path allocates in hot frames;
- camera hides route or target;
- haptics are decorative;
- movement ignores water/pressure/context;
- vehicle transitions pop or desync;
- high-end effects are required for control readability;
- no input/device proof exists after implementation.

## 7. Truth Ownership

Player truth is split deliberately:

- input system owns normalized device state and action bitmasks;
- movement owner owns body state, reference frame, velocity, stance, and interaction lock;
- `vehicles.md` owns platform-relative vehicle and docking math;
- camera consumes movement/tool/vehicle facts and writes no gameplay truth;
- haptics/audio/UI consume events and priority, not raw scene searches.

The player controller must not become a global coordinator for tools, vehicles, UI, damage, and camera.

## 7A. Presentation-Only Boundary

Camera shake, visor bob, sway, animation pose, tool recoil visuals, haptic texture, audio layers, screen effects, and cockpit UI are presentation-only. They may sell mass, pressure, fear, and machinery, but they must not own position, velocity, stance, interaction lock, oxygen, damage, vehicle authority, or tool result.

Presentation reads finalized movement/tool/vehicle/survival snapshots in visual sync. It must not write gameplay truth back into player movement, contact, resource, or damage state.

## 8. GlobalQualityWeight Scaling

Low/Middle/High/Ultra are continuous planning labels on the same `GlobalQualityWeight` curve, not binary switches:

- Low: control readability, route visibility, input buffering, core haptics, simple camera response.
- Middle: richer camera/audio/haptic layering, stronger tool stance cues, cleaner transition masking.
- High: better tool/vehicle feedback, physical micro-motion, improved contact presentation.
- Ultra: heavy sensory layering only after input clarity, comfort, and gameplay authority remain intact.

## 9. Proof Artifacts

Player-feel work must provide:

- input device matrix;
- movement state list;
- camera transition capture;
- haptic priority map if haptics changed;
- compact-tier readability proof;
- GC allocation proof for input/control hot path;
- vehicle/platform proof when reference frames are touched.

## 9A. First-20 Route Hook

The first-20 route must prove readable body control, oxygen-risk response, tool interaction cadence, route visibility, safe death/respawn feel, and at least one pressure/water/machinery feedback moment. Camera or haptic polish cannot compensate for weak movement authority.

Proof class: Play Mode/player capture for movement and feedback, Profiler/GCMonitor for input/control hot paths, screenshot for route readability, and save/load artifact for restored player position/state.

## 10. Acceptance Sentence

Player feel is accepted only when controls are predictable, heavy, readable, zero-GC, comfort-aware, and physically tied to water, pressure, tools, and vehicles.
