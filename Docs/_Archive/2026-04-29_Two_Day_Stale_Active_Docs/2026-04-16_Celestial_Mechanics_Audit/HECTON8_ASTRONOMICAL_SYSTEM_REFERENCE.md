# HECTON-8 Astronomical System Reference

Status: `ACTIVE`
Verification: `PENDING VERIFICATION`
Date: `2026-04-16`

Purpose:
- provide a lore-grade but implementation-usable astronomy baseline
- keep celestial art, lighting, shaders, and future narrative references on one numeric foundation
- separate fictional physical values from runtime visual compression

Important:
- these numbers are intentional project authoring values, not scientific claims about a real catalog object
- they are built to support the desired in-game feel: giant parent world, static horizon anchor, slow visible moon drift, and a believable oceanic moon fantasy

## 1. System Summary

Primary star:
- designation: `RS 437-2705-7-852476-1288`
- class target: late K / early M orange dwarf
- mass target: `0.62 solar masses`
- luminosity target: `0.034 solar luminosities`
- visual role: small warm sun with strong color contrast against the cold ocean world

Primary giant:
- name: `Aegir`
- class target: cool super-Jovian gas giant
- mass target: `12.8 Jupiter masses`
- radius target: `74,600 km`
- orbital distance from star: `0.186 AU`
- orbital period around star: `37.21 days`
- role in sky: fixed parent world dominating the horizon

Playable moon:
- working name: `Hecton-8`
- class target: oceanic rocky moon with deep atmosphere and global hydrosphere
- parent: `Aegir`
- orbital radius target: `392,930 km`
- physical orbital period target: `10.68 hours`
- lock state: tidally locked to `Aegir`
- player-facing sky result: Aegir remains nearly fixed in one sector of the sky while the sun still cycles

## 2. Why Aegir Looks Huge

With:
- `Aegir radius = 74,600 km`
- `Hecton-8 orbital radius = 392,930 km`

The parent giant presents a lore-facing apparent angular diameter of about:
- `21.5 degrees`

Current runtime visual compression:
- `32.25 degrees`

This is the current target band because it delivers the desired feeling:
- not a tiny postcard planet
- not a nearby wall
- a constant oppressive visual landmark that sells tidal lock and parent-world dominance

Recommended art band:
- lore baseline: `21.5 to 23.5 degrees`
- current runtime art target: `30 to 33 degrees`
- hard ceiling before surreal failure: `34 degrees`

Current atmospheric presentation contract:
- the gas giant and visible moons are intended to read behind the same sky-owned atmosphere veil
- `Sky_System` follows camera X/Z only; sky-rig height remains authored and no longer climbs with the player
- near-horizon veil is stronger, zenith veil remains present but weaker, and night veil is intentionally still non-zero so moons do not read as cutout UI discs

## 3. Visible Secondary Bodies

The current visible family is authored so that Hecton-8 sits in the middle of the broader moon system concept:
- inner siblings: `Pelagia`, `Varda`
- mid system anchor: `Ione`
- outer siblings: `Khepri`, `Thalos`, `Nammu`

### 3.1 Pelagia

Class target:
- small inner warm silicate moon
- closest bright daytime companion

Lore-scale target:
- diameter: `3,100 km`
- orbital radius around `Aegir`: `470,000 km`
- physical orbital period: `14.15 hours`

Current runtime visual authoring:
- apparent angular diameter: `0.34 degrees`
- apparent orbit radius around Aegir in sky space: `7.1 degrees`
- rendered apparent orbital period: `34,200 seconds` (`9.5 hours`)
- rendered axial rotation period: `34,200 seconds`

### 3.2 Ione

Class target:
- inner silicate moon
- bright warm-toned rocky body

Lore-scale target:
- diameter: `6,400 km`
- orbital radius around `Aegir`: `620,000 km`
- physical orbital period: `21.16 hours`
- expected behavior: relatively frequent motion and stronger phase readability than outer bodies

Current runtime visual authoring:
- apparent angular diameter: `1.18 degrees`
- apparent orbit radius around Aegir in sky space: `14.6 degrees`
- rendered apparent orbital period: `75,600 seconds` (`21.0 hours`)
- rendered axial rotation period: `75,600 seconds`

Reason:
- keeps movement visible over long play sessions
- avoids arcade-fast drift during a one-hour day cycle

### 3.3 Varda

Class target:
- dark carbon-rich inner-middle moon
- lower albedo and heavier dusk contrast than Ione

Lore-scale target:
- diameter: `4,900 km`
- orbital radius around `Aegir`: `560,000 km`
- physical orbital period: `18.38 hours`

Current runtime visual authoring:
- apparent angular diameter: `0.56 degrees`
- apparent orbit radius around Aegir in sky space: `10.2 degrees`
- rendered apparent orbital period: `52,200 seconds` (`14.5 hours`)
- rendered axial rotation period: `52,200 seconds`

### 3.4 Khepri

Class target:
- dusty amber middle-outer moon
- warmer body intended to read clearly in daylight without turning into a flat white disc

Lore-scale target:
- diameter: `5,200 km`
- orbital radius around `Aegir`: `760,000 km`
- physical orbital period: `29.36 hours`

Current runtime visual authoring:
- apparent angular diameter: `0.48 degrees`
- apparent orbit radius around Aegir in sky space: `19.7 degrees`
- rendered apparent orbital period: `111,600 seconds` (`31.0 hours`)
- rendered axial rotation period: `111,600 seconds`

### 3.5 Thalos

Class target:
- outer cold supermoon / ice-dominant body
- cooler tone, dimmer phase read, slower apparent drift

Lore-scale target:
- diameter: `8,600 km`
- orbital radius around `Aegir`: `980,000 km`
- physical orbital period: `42.05 hours`

Current runtime visual authoring:
- apparent angular diameter: `0.74 degrees`
- apparent orbit radius around Aegir in sky space: `24.9 degrees`
- rendered apparent orbital period: `151,200 seconds` (`42.0 hours`)
- rendered axial rotation period: `151,200 seconds`

### 3.6 Nammu

Reason for existence:
- user explicitly wanted a more shocking outer body in the system
- this slot acts as the large cold outer moon / captured heavy body of the visible family

Recommended target:
- class: cold outer supermoon with sub-Neptunian visual weight
- equivalent diameter target: `7,400 km`
- orbital radius around `Aegir`: `1,560,000 km`
- physical orbital period: `84.45 hours`

Current runtime visual authoring:
- apparent angular diameter: `0.86 degrees`
- apparent orbit radius around Aegir in sky space: `31.4 degrees`
- rendered apparent orbital period: `205,200 seconds` (`57.0 hours`)
- rendered axial rotation period: `205,200 seconds`

## 4. Gameplay Time Compression Contract

The project wants:
- one full day-night cycle in about `60 minutes`
- visible moon motion that is real enough to notice, but not so fast that bodies slide across the sky

Therefore two truths must exist at once:

1. lore / astronomy truth
- the system table above

2. runtime visible-motion truth
- damped or compressed apparent motion for player readability

Rule:
- never let runtime apparent motion become the only documented value
- always document both the lore period and the rendered apparent period

## 5. Render Targets

### 5.1 Aegir

Presentation goals:
- low horizon anchor
- lower hemisphere partially buried by ocean horizon
- stronger rim veil than center wash
- visible phase shaping at dawn and dusk
- clear feeling that the body is beyond local atmosphere

Current material direction:
- stronger rim veil
- stronger horizon veil
- reduced white wash in medium and upper haze
- slightly stronger backlight and terminator tint
- geometry sun asset remains in the scene as a legacy fallback, but the atmospheric sky-disc is now the only intended above-water solar image
- sky-disc energy and softness are now the primary authoring surface for the visible sun
- `Game Preview` now renders celestial bodies through `Main Camera` even when URP camera stacking is unavailable in the active renderer path

### 5.2 Moons

Presentation goals:
- phase driven by sun direction
- fill driven by Aegir direction
- subtle horizon haze so they do not read like local balloons
- different cadence and tone per body
- daytime crescents must retain faint full-disc presence instead of collapsing into black cutout circles

Current shader path:
- `HECTON/Celestial/Hecton_CelestialMoon`

## 6. Recommended Future Additions

- add one more distant body only after the current sky remains readable at dawn, noon, dusk, and eclipse cases
- generalize eclipse logic beyond Aegir only if the user explicitly wants visible moon transits
- keep Aegir fixed as the dominant tidal-lock landmark
- keep future distant bodies dimmer and smaller than Ione unless a special event needs them

## 7. Do Not Do

- do not derive surface-facing lore numbers from arbitrary scene mesh scale
- do not let moons use independent realtime clocks
- do not make visible moons complete obvious arcs during one gameplay day
- do not add more bright bodies until the sky remains readable under cloud cover and haze

## 8. Current Working Verdict

The system can support the user's target fantasy without full astrophysics simulation.

What matters is consistency:
- one time owner
- one angular placement model
- one documented set of lore numbers
- one separate set of runtime visual cadence values

That is enough to make the world feel intentional instead of fake.
