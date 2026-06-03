# HECTON-8 Lighting Bible

Status: AUTHORING LAW / STATIC DOC / RUNTIME PROOF NOT IMPLIED
Scope: authored lights, emergency lighting, instrument glow, bioluminescence, shadows, light eligibility, light baking, probe rules, darkness readability, and lighting proof gates.

## Prime Law

Light is scarce machinery. Darkness is pressure, not absence of art.

Every light must have a source, a reason, a state owner, and a gameplay readability job. HECTON-8 rejects generic blue ambience, clean sci-fi white rooms, random neon accents, bloom as identity, and black screens that hide unfinished geometry.

## Surface Light Boundary

Darkness-as-pressure applies to abyss, caves, industrial interiors, storms, and temporary eclipse windows. Surface, shoreline, ocean skin, photic shallows, sky, Aegir, and moons need motivated star, sky, atmospheric, and reflected light sufficient for beauty and navigation.

Surface lighting must not be black-crushed to imply mood. Use weather bands, glare, cloud shadow, radiation timing, wave state, and route risk for tension. Compact preserves readable surface silhouettes and ocean color; Middle adds richer cloud and shoreline response; High and Ultra add selective shafts, reflections, contact shadows, and cinematic celestial bounce without changing gameplay truth.

## Truth Ownership

Lighting owns visual illumination, shadow eligibility, baked/static light setup, probe placement, emissive material response, and light-state presentation. It does not own power truth, route truth, threat truth, biolum biology, damage truth, or objective state.

Power/logistics/survival/world owners publish state. Lighting consumes stable snapshots and chooses visual response. A flickering light that implies power failure must be backed by the owning system or marked as pure ambient damage with no gameplay promise.

## Light Source Taxonomy

Allowed primary families:

- route lights: small, motivated, directional, readable;
- work lights: harsh industrial pools near tools and maintenance access;
- instrument light: cyan/green state, never universal decoration;
- emergency light: amber/red only when state justifies it;
- search light: player/vehicle/tool-owned cone;
- biolum light: biological, contamination, threat, route clue, or evidence;
- welding/cutting light: intense, short, local, event-owned;
- distant practical light: proxy/emissive first, dynamic light only when near and useful.

## Shadow And Budget Rules

Dynamic shadows are expensive exceptions.

Required:

- shadow caster eligibility list;
- max active shadow lights per tier;
- no shadow on lights that do not affect route, threat, interaction, or readable scale;
- baked AO, decals, and material masks before screen-space overkill;
- conservative probe and reflection update cadence;
- no per-frame light creation/destruction in gameplay.

Compact uses static/baked/proxy light, limited shadows, and strong silhouettes. High tiers add local dynamic shadows where they change readability or fear.

## Bioluminescence

Bioluminescence is not decoration.

It must indicate biology, contamination, route, territorial warning, reproduction phase, feeding state, or instrument conflict. Phase, intensity, and color must come from authored/baked data or a macro owner. Random glowing dots are rejected.

## GlobalQualityWeight Scaling

`GlobalQualityWeight` may scale shadow count, contact shadow resolution, light cookie resolution, volumetric contribution, probe cadence, distant proxy richness, and secondary flicker layers. It must not change power truth, interaction eligibility, or player route truth.

Compact preserves route silhouettes and critical state colors. Middle adds richer local practical lights. High adds selective shadow and volume. Ultra adds cinematic richness only on lights with source, purpose, and proof.

## Production Packet

Any lighting, shadow, probe, bioluminescence, darkness, or motivated-light change must declare:

- light source owner and physical reason;
- range, intensity, color role, cookie/probe/shadow eligibility;
- compact no-black-screen capture;
- power/failure route if the light can change state;
- culling, LOD, and shadow demotion rules;
- affected UI, route, and interaction readability;
- Frame Debugger or RenderGraph proof if runtime rendering changed;
- profiler/GPU proof for dynamic or high-tier lighting.

Lighting that only makes the scene pretty while hiding route, scale, or interaction truth is rejected.

## Proof Artifacts

Lighting work must provide:

- light source manifest;
- owner/state list for dynamic lights;
- compact-tier screenshot;
- no-black-screen route readability capture;
- shadow eligibility list;
- Frame Debugger or RenderGraph proof if runtime passes changed;
- profiler/GPU proof for dynamic lighting changes;
- color role check against `TASTE.md` and `rendering.md`.

## Rejection Gates

Reject:

- generic blue ambient fill;
- bloom without motivated source;
- red/amber used as decoration;
- blackness hiding unfinished assets;
- dynamic shadows with no gameplay value;
- unbounded flicker or light updates;
- light state that lies about power, threat, or route.

## Acceptance Sentence

Lighting is accepted only when every important light has a source, state, budget, readability job, compact proof, and scalable path from survival silhouettes to high-tier visual overkill.
