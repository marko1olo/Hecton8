# HECTON-8 UI Generation Bible

Status: AUTHORING STANDARD - PENDING UNITY/PROFILER VERIFICATION
Owner: UI / Menu / Interface Standards
Scope: runtime HUD, main menu, pause menu, save/load UI, settings UI, terminals, cockpit/suit instruments, diegetic panels, and generated UI art assets.

## 0. Prime Law

HECTON-8 UI is an instrument, not decoration.

Every visible UI element must do at least one of these jobs:

- expose a player decision;
- report a physical state;
- prove route, pressure, oxygen, power, signal, hull, noise, salvage, or system trust;
- carry an interaction affordance;
- show failure evidence;
- guide the eye through a tense operation.

Decorative cyberpunk lines, random diagonals, empty grid fields, oversized flat buttons, fake telemetry, and unreadable microtext are rejected. A UI panel that looks technical but does not make the player understand or decide anything is production waste.

## 1. Routing Map

If a task asks an agent to create or improve UI, menus, HUD, cockpit panels, terminals, or visual interface taste, read this file first.

- Main menu, pause, settings, save/load, death/retry, boot screens, and modal screens: `UI_MENU_SCREEN_STANDARDS.md`
- Suit HUD, visor, cockpit, terminal, construction, map, scanner, diegetic dashboard, and world-space panels: `UI_DIEGETIC_HUD_STANDARDS.md`
- General project taste and rejection language: `taste.md`
- UI performance mandates: `.agents-skills/UI_Data_Streaming_ZeroGC_Optimization.txt`
- Physical diegetic interface mandates: `.agents-skills/UI_Diegetic_Physical_Interfaces.txt`
- Localization and font safety: `.agents-skills/UI_Localization_Babel_RTL_FontSwap_ZeroAlloc.txt`

This root file overrides weaker UI taste notes. Specialist files add stricter screen-family rules.

## 2. Screenshot Failure Inquisition

The provided screenshot fails HECTON-8 taste for these concrete reasons:

- It uses large decorative grid lines and diagonals that do not encode measurable pressure, route, signal, sonar, or system state.
- Buttons float as generic rectangles instead of feeling like physical controls, monitor prompts, panel keys, or operational commands.
- The composition has broad empty space without tension, depth, machinery, or navigational purpose.
- Typography scale is inconsistent: the brand is readable, but operational copy is too small and low-value.
- The cyan/orange palette is present but under-authored: color roles are not strict enough to distinguish command, warning, selected route, disabled state, or fatal state.
- The screen reads like a placeholder graphic rather than a fragile industrial instrument under pressure.

The fix is not "more lines". The fix is a functional interface model: physical substrate, state hierarchy, route logic, instrument behavior, readable control grouping, material treatment, and proof screenshots.

## 3. Interface Anatomy

Every production UI screen must declare:

- Physical carrier: visor glass, helmet HUD, cockpit panel, wall terminal, damaged monitor, handheld tablet, black-box replay, sonar station, command console, or dedicated boot console.
- Player task: descend, load archive, choose route, repair, scan, build, confirm risk, review failure, tune settings, recover evidence.
- Critical state: oxygen, pressure, hull stress, route, power, noise, signal, depth, salvage, save integrity, system trust, or access lock.
- Control model: keyboard/mouse, gamepad, controller raycast, physical switch, radial selector, dial, tab rail, list, map cursor, or confirmation hold.
- Failure state: disabled, corrupted, power loss, pressure lock, signal loss, unreadable, unsafe, no archive, no route, no oxygen, no permission.
- Proof view: screenshot at target aspect ratios and at least one low-tier readability capture.

Without this declaration, the UI is not ready for art.

## 4. Visual Language

HECTON-8 UI belongs to Deep Sea Noir and NASA-punk:

- dark graphite and abyssal green-black surfaces;
- cold cyan for measurement and sonar-like instrumentation;
- amber for service, route, warning, and action;
- red only for fatal or irreversible danger;
- off-white labels for stable readable text;
- dirty glass, subtle scanlines, rubbed paint, salt deposits, panel seams, small screws, gasket shadows, and worn labels;
- restrained motion, mechanical cadence, signal instability, and pressure-linked degradation.

Do not use:

- purple/blue sci-fi gradients;
- empty neon grids;
- decorative diagonal strokes;
- generic flat overlay panels;
- glow without physical source;
- rounded SaaS cards;
- toy-like large buttons;
- clean spaceship dashboards;
- "hacker terminal" noise that hides actual information.

## 5. Layout Law

Good UI is dense but not cluttered. It must have hierarchy:

- Primary decision region: one dominant command or current risk.
- State rail: concise physical telemetry that changes player judgement.
- Navigation region: tabs, route map, list, or instrument selector.
- Context region: short explanation only when the player needs it.
- Failure region: explicit fault, lockout, missing resource, or corruption.

Layout rules:

- Use a stable 8 px or 4 px baseline grid in screen-space equivalents.
- Align every edge to a reasoned column, panel seam, instrument rail, or physical cut line.
- Never place text on decorative graphics that reduce readability.
- Never put decorative elements in the same visual priority as controls.
- Keep all controls reachable by keyboard/gamepad and readable at 720p.
- Reserve huge type for screen title, current operation, or fatal warning only.
- Long text belongs in terminal/log screens, not primary HUD/menu states.

## 6. Typography Law

Text is short because the player is under pressure.

Required:

- One primary display family: technical mono or narrow industrial sans with high legibility.
- One secondary label family only if needed.
- Fixed text roles: title, section, command, label, readout, warning, log, disabled, tooltip.
- Pre-baked font atlas for HUD/menu core text.
- Localization expansion proof for German/Finnish-like long text and RTL/CJK where applicable.

Rejected:

- random font mixing;
- tiny unreadable flavor labels;
- all-caps paragraphs;
- glowing text with poor contrast;
- runtime string formatting in HUD paths;
- text that explains what the visual state should have shown.

## 7. Color And Contrast Law

Color roles are semantic:

- Cyan: measurement, sonar, scan, stable system readout.
- Amber: action, service, route, caution, selected nonfatal operation.
- Red: fatal, pressure breach, oxygen collapse, irreversible action.
- Desaturated green: nominal machine state only.
- Off-white: labels and primary readable copy.
- Graphite/deep teal: panel substrate.

Do not use color only. Pair color with shape, label, icon, motion, or position.

Contrast requirements:

- Primary commands and fatal warnings must be readable on 720p captures.
- Disabled states must read disabled without becoming invisible.
- Background decorative lines must never exceed the contrast of real controls.
- UI over scene must pass against black water, lit metal, fog, and emergency lighting.

## 8. Interaction Law

Controls must feel like operations:

- Use hold-to-confirm for dangerous actions.
- Use toggles, levers, dials, segmented controls, tabs, route rails, and physical switch metaphors where the task implies machinery.
- Use buttons only for clear commands.
- Every selected state must show control ownership.
- Every disabled state must state why in one short phrase or icon+tooltip.
- Every destructive action must show consequence, not legalistic filler.

Do not make every command the same rectangle. Repeated rectangular buttons are allowed only in lists, settings rows, save slots, or modal commands where uniform scanning matters.

## 9. Motion And Failure

Motion is information:

- power-up scan;
- sonar sweep;
- pressure warning pulse;
- route recalculation;
- save verification;
- damaged panel flicker;
- signal dropout;
- lockout click;
- archive read corruption;
- oxygen/hull warning cadence.

Rejected:

- constant ambient glitch;
- random shake;
- looping noise that never means anything;
- animated decoration that competes with controls;
- UI effects that cost frame time but do not sharpen a decision.

Motion must be load-sheddable through `GlobalQualityWeight`. Compact tier keeps stillness, silhouettes, color roles, and concise state. Ultra adds richer glass, scanlines, secondary motion, and material response without changing truth.

## 10. Unity Runtime Law

Interactive runtime UI should be diegetic or physically anchored by default:

- World-space canvas or mesh/RT projection for interactive panels.
- Physical screen, visor plane, cockpit display, tablet, terminal, or command console anchor.
- No `RenderMode.ScreenSpaceOverlay` for interactive first-party gameplay UI.
- Flat screen-space UI is allowed only for noninteractive debug, loading, legal/accessibility, or explicitly approved frontend bridge screens.
- Runtime `OnGUI()` is forbidden.

Hot path performance:

- No `SetActive` toggling for active UI. Use `CanvasGroup.alpha`, `blocksRaycasts`, and state flags.
- No `TMP_Text.text = ...` in HUD/update paths.
- Use baked integer hashes, preallocated char buffers, `TryFormat`, and `TMP_Text.SetCharArray`.
- Separate static, low-cadence, and high-cadence canvases.
- Disable raycast targets on noninteractive graphics.
- Do not run hierarchy searches or string-key localization in UI updates.
- Do not allocate RenderTextures dynamically in gameplay; pool them or create them during approved load/setup.

## 11. Texture And Asset Law

UI art assets must be authored like production assets:

- UI atlases use mipmaps off unless world-space distance sampling demands otherwise.
- Keep UI atlas family sizes within platform budget.
- Do not ship uncompressed UI textures without written proof.
- Use signed distance fonts and static font atlases for core HUD/menu text.
- Use mask textures for glass dirt, scanlines, vignette, scratches, and panel wear.
- Do not generate unique texture variants for every menu state when one atlas plus material parameters can carry it.
- UI icons must be functional symbols, not decorative filler.

## 12. Generated UI Implementation Order

1. Declare screen family and physical carrier.
2. Define player task and critical state.
3. Define information hierarchy and input model.
4. Build wireframe with real labels and representative long localized strings.
5. Apply visual language: substrate, typography, color roles, controls, failure states.
6. Add motion only where it reports state.
7. Implement performance-safe update cadence and zero-GC text route.
8. Add accessibility and localization expansion gates.
9. Capture proof at 16:9, 16:10, 21:9, 4:3, 720p, and target low-tier render scale.
10. Reject if screenshot fails task clarity, taste, or performance.

## 13. UI Acceptance Gates

Reject if:

- screenshot still reads as a generic sci-fi overlay;
- controls are floating rectangles with no physical or operational logic;
- decorative grid/lines are stronger than real information;
- player task is unclear in 3 seconds;
- text overlaps, clips, or becomes unreadable at 720p;
- disabled state lacks reason;
- color roles are inconsistent;
- localization breaks layout;
- UI update path allocates GC;
- `SetActive`, `TMP_Text.text`, `OnGUI`, scene search, or runtime string formatting appears in hot paths;
- no low-tier screenshot exists;
- no render/performance status is recorded.

If it looks cool but does not make the player decide, distrust, retreat, repair, reroute, scan, save, or descend, it is rejected.

## 14. Truth Ownership

UI owns presentation, input focus, layout, control affordance, text rendering, and state hierarchy. UI does not own oxygen, pressure, hull, route, AI, save, mission, construction, tool, vehicle, or inventory truth.

Every UI readout must name its source owner. If the source is stale, missing, corrupted, or low-confidence, the UI must show that state instead of pretending certainty.

## 15. GlobalQualityWeight Scaling

Compact keeps hierarchy, legibility, color roles, static atlases, zero-GC text, and reduced motion. Middle adds richer glass, route diagrams, and screen material. High adds better degradation, secondary display response, and smoother transitions. Ultra adds dense instrument detail and cinematic screen texture without changing the command model or state truth.

## 16. Proof Artifacts

UI work must provide:

- screen family and physical carrier;
- source owner list for critical readouts;
- input navigation proof;
- 720p screenshot;
- long localized string proof;
- disabled/failure state screenshot;
- zero-GC text/update route;
- compact-tier screenshot;
- accessibility note.

## 17. Acceptance Sentence

UI is accepted only when it behaves like an instrument, exposes true state from named owners, stays readable under pressure, supports input/accessibility, proves low-tier layout, and avoids hot-path allocation.
