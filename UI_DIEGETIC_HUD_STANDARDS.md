# UI_DIEGETIC_HUD_STANDARDS

Status: AUTHORING STANDARD - PENDING UNITY/PROFILER VERIFICATION
Scope: suit HUD, visor overlays, cockpit panels, terminals, scanners, construction UI, map/sonar, interaction prompts, warning displays, world-space panels, and physical UI controls.

## 0. Prime Diegetic UI Law

Diegetic UI is a survival instrument, not decoration. It must make the player's next physical decision clearer: breathe, turn back, scan, repair, cut, dock, route, hide, confirm, or abort.

The interface may be beautiful, damaged, noisy, wet, projected, cracked, or embedded in machinery, but it must remain readable, localized, operable, zero-GC in hot paths, and truthful to its owner data. A stylish overlay that hides state, invents safety, clips localized text, or works only in screenshots is rejected.

## 1. Diegetic Carrier Law

Interactive gameplay UI should be bound to a physical carrier:

- helmet glass;
- wrist/tablet device;
- cockpit panel;
- submarine console;
- wall terminal;
- hand tool display;
- scanner overlay;
- sonar station;
- construction projector;
- damaged black-box playback device.

The carrier defines the UI. A terminal can be blocky and monochrome. A visor can be projected and cracked. A cockpit panel can use physical switches and monitor RTs. A handheld scanner can be small, noisy, and tool-focused. Do not use one generic overlay for all of them.

## 2. HUD Information Hierarchy

HUD shows only what changes player behavior:

- oxygen and breathable reserve;
- hull/pressure stress;
- depth/route;
- tool state;
- scan target;
- signal/noise/trust;
- warning state;
- interaction affordance;
- return path or anchor cue when relevant.

Everything else belongs in menus, terminals, scanner detail, or map screens.

## 3. State Ownership

UI is presentation. It does not own gameplay truth.

Required:

- read immutable snapshots, cached owner interfaces, or typed signal packets;
- no scene searches in UI update;
- no polling GlobalRegistry in hot paths;
- no direct mutation of simulation state from read accessors;
- write commands through explicit owner interfaces or typed signal lanes.

UI must fail closed. If data is stale, show stale state, fault marker, or no reading. Do not invent safe numbers.

## 4. World-Space Panel Law

Interactive panels use world-space or physical projection:

- panel transform is bound to geometry;
- ray-to-panel hit math uses cached transforms or approved interaction service;
- controls have physical depth, frame, glass, screws, gasket, hinge, socket, or screen substrate;
- cursor and hover states stay within panel bounds;
- panel sleep/resolution tier scales with distance;
- far panels suspend expensive updates.

Screen-space interactive overlays are rejected for first-party gameplay unless explicitly approved as a non-diegetic bridge.

## 5. Readout Cadence

Not every number updates at 60Hz:

- 60Hz: reticle, immediate critical warning flash, direct aim/interaction cursor.
- 10Hz: oxygen, depth, pressure, hull, power, signal, scanner progress.
- 2Hz: memory/build/debug, archive checksum, long-range route stats.
- Event-driven: save complete, door lock, breach, repair complete, item acquired, terminal unlock.

Use hysteresis and thresholds so UI feels instrumented, not twitchy.

## 6. Zero-GC Text

HUD and repeated UI updates must be zero allocation:

- baked integer localization keys;
- preallocated char buffers;
- `TryFormat` for numeric values;
- `TMP_Text.SetCharArray`;
- registry lookup by baked hierarchy hash;
- no `TMP_Text.text =`;
- no interpolated strings;
- no `string.Format`;
- no runtime hierarchy path building;
- no `FindObjectsOfType` or `GameObject.Find`.

If a text update allocates in HUD/update path, the UI is rejected.

## 7. Warning Design

Warnings must be legible and disciplined:

- amber for caution/service;
- red for fatal/urgent only;
- audio/visual/haptic pairing for critical states;
- short label plus physical value when possible;
- cadence encodes severity;
- repeat suppression prevents warning spam.

Bad warning UI:

- large generic flashing overlays;
- unreadable glitch;
- all warnings same color;
- warning copy that does not name the physical problem.

## 8. Scanner, Sonar, And Map

Scanner/sonar UI must trade certainty for tension:

- show partial facts;
- show confidence, noise, range, occlusion, or stale state;
- use sweeps and returns only where they represent signal behavior;
- do not reveal hidden threats cleanly without earned data;
- use silhouettes, pings, bearing arcs, depth bands, and route cues.

A map that is only a pretty grid is rejected. A grid must carry position, scale, route, uncertainty, obstruction, or scan confidence.

## 9. Construction And Inventory UI

Construction UI must show:

- object silhouette or schematic;
- required resources;
- missing resources;
- power/pressure/space constraints;
- valid/invalid placement reason;
- preview state;
- confirm/cancel controls.

Inventory UI must show:

- carried mass/volume if relevant;
- oxygen/power/salvage trade if relevant;
- item condition;
- tool compatibility;
- immediate action.

Do not turn survival systems into colorful loot cards.

## 10. Visual Damage And Degradation

UI damage is allowed only when it informs:

- cracks at visor edge should not hide critical text;
- pressure stress can introduce subtle jitter or text decay;
- power loss can lower refresh and dim panels;
- water damage can corrupt noncritical regions;
- signal loss can degrade scanner confidence.

Do not use decorative glitch. A damaged interface still has to be usable.

## 11. Accessibility And Localization

Required:

- UI scale support;
- subtitle and warning readability;
- colorblind support through shape/text, not palette only;
- reduced flashing mode;
- font atlas strategy for supported languages;
- expansion tests for long strings;
- controller and keyboard navigation.

Accessibility is not a softness pass. It is instrument reliability.

## 12. GlobalQualityWeight Scaling

`GlobalQualityWeight` may scale visor glass detail, scanline fidelity, panel render resolution, scanner echo richness, warning animation smoothness, screen dirt, secondary telemetry density, and diagnostic overlays. It must not change gameplay truth, warning priority, command routing, localization availability, input semantics, or critical readout cadence.

Compact HUD keeps oxygen, pressure, route, tool state, warnings, interaction affordance, and return cue readable at low resolution. Middle adds richer material and scanner confidence display. High adds better panel damage, cockpit lighting response, and state transitions. Ultra adds layered screen artifacts and cinematic carrier detail only around stable readable text and controls.

## 13. Diegetic UI QA Gates

Reject if:

- UI owns simulation truth;
- UI update path allocates GC;
- interactive gameplay UI is generic screen overlay without approved bridge;
- readout update cadence is unjustified;
- scanner/map grid has no data meaning;
- warnings are noisy instead of informative;
- text clips at 720p;
- UI cannot be operated by keyboard/gamepad/controller route;
- low-tier screenshot loses critical state;
- Frame Debugger/Profiler status is not recorded after implementation.

## 13A. Proof Artifacts

Diegetic UI work must provide:

- physical carrier description or screenshot;
- owner-data route and stale/fault display behavior;
- hot-path text/formatting allocation proof when runtime UI changed;
- update cadence table for every repeated readout;
- compact screenshot proving oxygen/pressure/route/tool/warning/interaction readability;
- normal/high-tier screenshot when visual material richness is claimed;
- keyboard/gamepad/controller navigation proof for interactive panels;
- localization expansion, RTL/CJK/fallback risk note where text appears;
- profiler/Frame Debugger proof when render textures, panel cameras, shader effects, or runtime UI paths changed.

## 14. Acceptance Sentence

A diegetic HUD or world panel is accepted only when it is bound to a believable physical carrier, reads immutable owner truth, updates at justified cadence, allocates zero GC in hot paths, scales through `GlobalQualityWeight` without changing warning or command truth, and proves compact readability plus profiler state for runtime changes.
