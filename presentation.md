# HECTON-8 Presentation Bible

Status: AUTHORING STANDARD - PENDING UNITY/PROFILER VERIFICATION
Scope: lighting, fog, post-processing, VFX, particles, camera, screenshots, trailer capture, scene composition, and render taste.

## 0. Prime Presentation Law

Presentation must reveal pressure, machinery, route, scale, and evidence. It must not hide weak assets behind darkness, fog, bloom, or glitch.

Visual quality is not a post stack. It is composition plus material truth plus readable silhouettes plus controlled sensory pressure.

## 1. Noir Lighting

Lighting should be scarce, motivated, and useful:

- route lights;
- work lights;
- instrument glow;
- emergency amber/red;
- biolum evidence;
- welding/cutting;
- sonar/scan response;
- external search cones;
- damaged flicker from power state.

Reject generic blue ambience, one-note darkness, and clean sci-fi white rooms. Pure black is not mood; it is missing information.

## 2. Fog And Water

Fog stages decisions:

- preserve a readable near/mid/far structure;
- reveal silhouettes before detail;
- use LUT/depth/noir remap on low tier;
- reserve expensive volumetrics for proven tiers;
- avoid aquarium haze.

Fog that hides navigation, weak assets, or lack of environment is rejected.

## 3. VFX

VFX are consequences:

- leak;
- spark;
- silt wake;
- sonar pulse;
- pressure crack;
- tool heat;
- weld plume;
- bubble burst;
- vent emission;
- creature disturbance.

Do not simulate invisible causes when a shader/audio/haptic fake carries the belief. Do not emit particles just to fill a screen.

## 4. Camera

Camera supports vulnerability:

- tight spaces feel heavy;
- movement remains readable;
- shake is event-based and load-sheddable;
- FOV supports tools and route reading;
- cutscenes preserve physical continuity where possible;
- camera never hides player decisions.

Bad camera:

- constant shake;
- cinematic drift without gameplay information;
- hero framing that makes the threat harmless;
- UI/menu camera that looks like a website background.

## 5. Screenshot Composition

A production screenshot must show at least one:

- player verb;
- pressure cue;
- machine cue;
- route cue;
- scale cue;
- danger cue;
- evidence cue;
- instrument corruption cue.

If a screenshot only shows mood, it is not enough. Beauty must be attached to risk or decision.

## 6. Color And Materials

Palette discipline:

- abyssal black-green floor;
- cyan measurement;
- amber service/warning;
- red fatal;
- off-white labels;
- oxidized metal;
- wet basalt;
- salt and silt.

Avoid purple/blue gradient sci-fi, clean plastic, glowing surfaces with no source, and white albedo pretending to be brightness.

## 7. Render Performance

Presentation must follow:

- URP-only;
- SRP Batcher;
- RenderGraph for new features;
- no Bloom on compact;
- no URP SSAO;
- no hidden `Graphics.Blit` chains;
- no per-frame reflection probe refresh;
- no expensive VFX without load-shed;
- no feature over 0.1 ms without profiler proof and fallback.

Compact must still look intentional through LUTs, silhouettes, authored lights, baked AO, dither, and material masks.

## 8. Presentation QA Gates

Reject if:

- darkness hides missing content;
- fog destroys route readability;
- VFX has no state cause;
- camera motion has no player value;
- screenshot lacks decision/evidence;
- post stack does the work of art direction;
- low-tier capture collapses into mud;
- profiler/render proof is absent after implementation.
