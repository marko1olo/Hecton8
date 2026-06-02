# UI_MENU_SCREEN_STANDARDS

Status: AUTHORING STANDARD - PENDING UNITY/PROFILER VERIFICATION
Scope: main menu, boot flow, pause, settings, save/load, death/retry, confirmation dialogs, frontend route screens, and non-HUD menu states.

## 1. Menu Identity

HECTON-8 menus must feel like operational consoles, black-box systems, damaged boot sequences, cockpit panels, dock terminals, suit firmware, or archived mission interfaces. They must not feel like a website, generic game launcher, or empty sci-fi wallpaper.

The menu is part of the world. It should imply:

- who built the system;
- what physical machine carries it;
- what state the mission is in;
- what pressure, route, or archive is being trusted;
- what can fail before gameplay starts.

## 2. Main Menu Required Structure

The main menu must present:

- identity: HECTON-8 title or current mission system;
- primary command: descend, continue, resume, load, or begin operation;
- archive command: load/save/codex/black-box as appropriate;
- route/state readout: current route, depth, pressure range, build/profile, or system integrity;
- secondary access: settings, credits, quit, accessibility;
- visual substrate: dock console, suit visor, terminal, black-box playback, cockpit monitor, or physical panel.

The first screen must not be a decorative landing page. It is a working instrument.

## 3. Screenshot-Specific Rejection Rules

Do not repeat the screenshot failure pattern:

- no large empty grid field without data;
- no random diagonal cyan/orange lines;
- no floating blocks labeled like web tabs unless they are physical console segments;
- no huge blank negative space that neither frames a route nor creates pressure;
- no microcopy too small to matter;
- no color accents without role;
- no brand title as the only polished object.

If a grid appears, it must be a map, sonar trace, pressure chart, route planner, signal diagnostic, archive timeline, or docking alignment instrument. Every line must have a job.

## 4. Main Menu Composition Pattern

Approved high-quality pattern:

- left or lower-left operation stack with primary command and archive/settings below;
- central physical monitor or route instrument with real telemetry;
- right-side or top-side status rail for environment/system state;
- bottom rail for build/profile, accessibility hint, input legend, and warning state;
- background scene or physical console, not a flat gradient;
- controlled glass/screen material: dirt, scratches, scanline, slight bloom only where budget permits.

Do not center all buttons in a clean column unless the physical carrier is a narrow terminal and the rest of the screen provides meaningful instrument context.

## 5. Commands And Button Design

Commands must look built:

- primary command uses a clear control shape, stronger luminance, and one action verb;
- secondary commands are quieter and grouped by function;
- dangerous commands use hold/progress or confirmation;
- disabled commands show why, such as `NO ARCHIVE`, `PRESSURE LOCK`, `NO SIGNAL`, or `ACCESS LOST`;
- selected state must be visible without relying only on color.

Button rectangles need bevels, brackets, notches, slot cuts, scan ticks, physical margins, or panel embedding. A flat filled rectangle is allowed only as a temporary wireframe or a deliberate dead-simple terminal key.

## 6. Settings Menu

Settings are not a dumping ground.

Required groups:

- Video: render scale, resolution, fullscreen, brightness, contrast, motion effects, post stack, UI scale.
- Controls: input device, sensitivity, axis inversion, remap, hold durations.
- Audio: master, suit voice, ambience, warning voice, dynamic range, headphone mode.
- Accessibility: subtitles, color aids, font scale, flashing reduction, hold-to-press alternatives.
- Gameplay presentation: camera shake, visor dirt intensity, UI degradation intensity, tutorial prompts.

Each row must use the correct control type: toggle, slider, stepped selector, dropdown/segmented control, keybind row. Do not make settings as a stack of generic buttons.

## 7. Save And Load

Save/load UI must feel like archive integrity:

- slot name;
- timestamp;
- depth/zone/route;
- oxygen/power/hull snapshot if relevant;
- corruption or mismatch state;
- screenshot thumbnail or symbolic archive card if available;
- clear action: load, overwrite, delete, repair/import if supported.

Archive cards can be damaged, wet, corrupted, or incomplete, but the load action must remain readable. Decorative corruption cannot hide critical slot data.

## 8. Pause And Death

Pause is an operational interruption, not a clean escape:

- show current system state and why pausing is limited or safe;
- resume is primary;
- settings and quit are secondary;
- if diegetic pause is impossible in a scene, use a stark system overlay with minimal noise.

Death/retry must show evidence:

- cause category;
- last known depth/pressure/oxygen/hull/signal;
- recovered black-box fragment or telemetry summary;
- retry/load/exit commands;
- no melodramatic copy.

## 9. Confirmation Dialogs

Confirmations must be short and physical:

- one sentence of consequence;
- one primary command;
- one cancel command;
- hold-to-confirm for destructive or irreversible actions;
- optional small telemetry line if the decision changes route, oxygen, save, power, or pressure.

No generic "Are you sure?" unless the consequence is already visible.

## 10. Menu Motion

Allowed:

- boot scan;
- archive verify pulse;
- route plot;
- warning pulse;
- save checksum sweep;
- terminal focus travel;
- damaged screen flicker tied to power/hull state.

Rejected:

- constant animated background lines;
- generic glitch loops;
- particles behind menu with no physical source;
- motion that makes text harder to read;
- menu intro longer than player tolerance.

## 11. GlobalQualityWeight Scaling

`GlobalQualityWeight` may scale screen material richness, scanline fidelity, background scene detail, route-instrument density, transition smoothness, archival corruption layers, and optional diagnostic overlays. It must not change command order, save/load truth, settings semantics, accessibility availability, input navigation, or text readability.

Compact menus must still read as a physical HECTON-8 instrument, not a plain flat launcher. Middle may add stronger panel material and route telemetry. High may add richer boot/verify transitions. Ultra may add cinematic console material, live background response, and layered archive damage only if it remains readable and zero-GC in runtime menu paths.

## 12. Menu QA Gates

Reject if:

- menu can be mistaken for a generic sci-fi template;
- primary command is not readable in 3 seconds;
- settings controls are wrong type;
- save/load lacks state proof;
- text clips at 720p or with localization expansion;
- color roles do not match `ui.md`;
- interactive UI violates diegetic/performance rules without approved bridge reason;
- no screenshot exists for compact and normal layouts.

## 13. Acceptance Sentence

A menu screen is accepted only when it reads as a physical HECTON-8 operating surface, presents the primary command within 3 seconds, preserves settings/save/accessibility truth, scales through `GlobalQualityWeight` without changing command semantics, and has compact plus normal screenshots proving text, controls, and state remain readable.
