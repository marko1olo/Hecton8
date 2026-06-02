# Aegir System - Game Texture

Status: working world texture.
Purpose: make Aegir, its moons, and orbital mechanics player-facing without requiring live celestial simulation.

## Core Feeling

Aegir should be huge, present, and useful to the game.

It is not a black void backdrop. It is a gas giant with weather bands, magnetospheric danger, moon shadows, reflected light, and route geometry. HECTON-8 is an ocean moon inside a moving system, not a sealed aquarium.

## What The Player Sees

Clear Weather:
Aegir fills part of the sky or horizon. Its light changes water color and shallow visibility.

Storm Weather:
The giant vanishes behind cloud and rain. Uplink windows become worse.

Moon Crossings:
Other moons pass, eclipse, occult, or alter tide/signal states.

Magnetic Storm:
HUD and radio degrade. Not supernatural. Aegir is loud.

Low Orbit Debris:
Rare streaks, old hardware burns, or carrier glints in late-game sky events.

## Gameplay Roles

Uplink Timing:
Certain windows allow stronger Black Keel contact, data burst, or evidence upload.

Tide / Current Shifts:
Moon geometry opens or closes shallow routes, pressure gates, brine layer access, or cave flows.

Radiation / Magnetic Noise:
Forces shelter timing, instrument errors, signal decay, and hard choices about sending packets.

Ascent Planning:
Escape is not only repair. It is also timing with sky, weather, carrier geometry, and payload mass.

Atmosphere / Weather:
Storms justify broken descent, bad uplinks, dangerous surface operations, and shifting shallow hazards.

## Aegir Visual Modes

Low Quality:
Static sky states, simple light/color changes, UI window timer.

Middle:
Animated cloud bands, moon icons, tide state markers, radio distortion.

High:
Visible moon transits, eclipse lighting, improved storm fronts, carrier glints, radiation shimmer.

Ultra:
Dense sky detail, layered cloud motion, orbital debris streaks, high-fidelity transition lighting, richer signal artifacts.

Truth does not change by quality level.

## Moon Roles

Inner Hot Moon:
Volcanic/thermal. Good for sky drama and old route warning logs.

Shepherd Ice Moon:
Small, bright, affects ring/debris lore if used.

Relay Moon:
Carries old comm or navigation infrastructure. Can justify why windows work at all.

Dead Claim Moon:
Failed site, useful for Marauder rumors and Black Keel old routes.

HECTON-8:
Ocean moon, not closest and not farthest. Valuable because of pressure chemistry, Deep Reach infrastructure, Atlas-6, and the colony disaster.

Outer Survey Moon:
Remote, cold, mostly machines. Useful as contrast and late website/wiki article material.

## Source Text Hooks

Scanner - Aegir Clear:
Aegir visible through storm break. Signal noise is falling, but radiation count is rising.

Scanner - Moon Shadow:
Moon occultation in progress. Local tide model unreliable for the next window.

PDA Note:
On HECTON-8, weather is local and orbital at the same time. A clear surface does not mean a clean sky.

Marauder Field Note:
If the giant is pretty, check the radio. Pretty usually means charged.

Black Keel Line:
Recovery geometry remains unfavorable. Next ascent-mass estimate updates after moon shadow clears.

## Procedural Use

Use authored sky/window tables per seed:

- comm windows;
- tide states;
- storm bands;
- moon visibility;
- rare orbital events.

Do not require live N-body. The player needs believable timing and repeatable rules, not a physics thesis running every frame.
