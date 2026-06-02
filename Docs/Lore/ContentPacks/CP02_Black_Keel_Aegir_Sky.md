# CP02 - Black Keel And Aegir Sky

Status: source draft.
Packet Family: carrier / orbit / sky pressure.
Canon Owner: Black_Keel_Ownership, Aegir_Moon_Catalog, Gameable_World_Packets P02/P12/P13.
Runtime Layer: Narrative for calls, World for sky/moon scanner tags.
Primary Surfaces: carrier transcript, orbital UI, scanner sky tags, PDA.

## Player Experience

The sky is a machine the player cannot touch.

Aegir fills the horizon when weather clears. Other moons move. The Black Keel answers in fragments, sometimes useful, sometimes cold, sometimes clearly routed through a contract priority the player did not approve.

The player should feel:

- the carrier is real;
- rescue is physically possible;
- rescue is not currently available;
- every message has a price.

## Core Objects

Black Keel:
Automated or near-empty claim-tender. Not a friendly home, not a dead ship.

Orbital Window Clock:
Shows when uplink, descent weather, ascent attempts, or data bursts are possible.

Signal Mast:
Repairable shallow/deep infrastructure. Turns sky timing into gameplay.

Moon Tags:
Visible moons or occultation warnings. They can affect tide, signal, and route access.

Contract Buffer:
Local cached contract state. It can update after comm windows and change pressure on the player.

## Beat Chain

1. Player repairs first usable uplink path.
2. Black Keel answers with corrupted handshake.
3. It confirms player survival but does not rescue.
4. It asks for claim status, sample state, or coordinates before medical state.
5. Player sees Aegir/moon window timer.
6. Later windows bring new messages: broker, automated system, Deep Reach priority, old route data.

## Seeded Variants

Window Timing:
Short frequent weak windows, rare strong windows, long blackout, storm-corrupted windows.

Carrier Voice:
Pure automation, broker relay, old claim-pool AI, Deep Reach-filtered response.

Sky Pressure:
Eclipse, radiation warning, tide shift, magnetic noise, visible moon conjunction.

Player Choice:
Report sample, hide sample, send evidence, ask extraction, spoof damaged telemetry.

## Text Drafts

Title:
Black Keel Contact.

Scanner Short - Aegir:
Gas giant above horizon. Magnetosphere and moon geometry are interfering with clean transmission.

Scanner Short - Signal Mast:
Old relay mast. Salted, bent, still pointed at the right sky.

Marauder Field Note:
When a carrier asks what you found before it asks if you are breathing, you are not the client. You are the tool.

PDA Codex Body:
The Black Keel is a system carrier, not an interstellar rescue ship. It moves claims, capsules, cargo tugs, sealed samples, and unlucky operators through Aegir traffic. Its orbit is chosen for cost, geometry, and contract coverage, not for the comfort of someone stranded below cloud and water.

Contact depends on the sky. Aegir's magnetosphere, HECTON-8 weather, moon positions, and old relay alignment can turn a clear sentence into a block of broken acknowledgements. A strong window is rare enough that every packet matters.

Carrier Transcript - First Contact:
OPERATOR STATE: alive.
DROPCRAFT STATE: unrecoverable.
CLAIM STATE: open.
SAMPLE STATE: unknown.
RECOVERY STATE: pending window.
ACTION REQUIRED: restore uplink confidence.

Carrier Transcript - Suspicious Later Contact:
Your medical status is noted. Priority request remains unchanged: confirm coordinates of high-pressure substrate exposure.

Audio Beat:
Window is closing. Send one packet. Choose before the storm band rolls over the mast.

Website Public:
The Black Keel is the player's only line back to orbit, but not a safe haven. Its messages arrive through weather, moon geometry, and contract logic, turning the sky into a survival system.

## Gameplay Use

This pack makes the sky playable:

- uplink repair;
- timed message windows;
- limited bandwidth choices;
- carrier pressure;
- first hint that someone values samples more than the player.

## Runtime Notes

Use authored ephemeris/window data. No live N-body requirement. Seed can vary window order and sky presentation, while the carrier's structural role stays fixed.
