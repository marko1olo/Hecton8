# HECTON-8 Audio Bible

Status: AUTHORING STANDARD - PENDING UNITY/PROFILER VERIFICATION
Scope: soundscape, sonar, hydrophones, warnings, suit voice, creatures, machinery, UI audio, music discipline, mix states, and audio performance taste.

## 0. Prime Audio Law

Sound arrives first. HECTON-8 should often make the player afraid or informed before the image explains why.

Every audio cue must carry one of these functions:

- route information;
- threat presence;
- pressure state;
- machine state;
- oxygen/suit state;
- signal trust;
- tool feedback;
- evidence memory;
- warning priority.

Generic ambience, generic monster sounds, clean sci-fi beeps, and constant music beds are rejected.

## 1. Soundscape Layers

A scene mix should have:

- Abyss bed: pressure rumble, low current, hull stress, distant water body.
- Machine layer: pump, relay, fan, valve, hatch, conduit, ballast, power hum.
- Signal layer: sonar, hydrophone, radio carrier, black-box fragment, scanner ping.
- Threat layer: partial creature signal, scrape, displacement, breath, silence break.
- Player layer: suit breath, tool, warning, body, interaction.
- Evidence layer: old audio logs, terminal playback, corrupted fragments.

Do not let all layers speak at once. Mix state must prioritize current player decision.

## 2. Sonar And Hydrophone

Sonar is partial truth:

- bearing before identity;
- confidence before certainty;
- occlusion and stale data;
- active ping creates risk;
- passive listening is safer but weaker;
- returns must respect environment and creature behavior.

Clean omniscient radar is rejected. Pure decorative pings are rejected.

## 3. Warnings And Suit Voice

Warnings must be sparse, prioritized, and physical:

- name the system;
- state the severity;
- use cadence and tone by priority;
- suppress spam;
- pair critical warnings with UI/haptic where appropriate;
- fail closed if warning data is stale.

Suit voice should be disciplined, not chatty. It should sound like expensive equipment under stress, not a joke machine.

## 4. Creature Audio

Creature audio must shape behavior:

- calls imply distance, size, mood, or territory;
- movement sounds indicate route risk;
- silence can be a cue;
- reactions should tie to noise, light, blood, power, hull stress, or intrusion;
- full creature vocal reveal should be earned.

Generic roar libraries are rejected unless transformed into a unique underwater/acoustic identity.

## 5. Machinery Audio

Machines are verbs:

- pump starts, stutters, cavitates, and fails;
- pressure door seals, grinds, locks, or jams;
- relay hum shifts under load;
- power route changes soundscape;
- damaged panels buzz, click, leak, or arc.

A machine with no sound state feels decorative.

## 6. Music Discipline

Music must not flatten dread:

- use silence and low-frequency pressure first;
- music enters when it sharpens a decision or transition;
- avoid constant beds that tell the player how to feel;
- avoid heroic comfort unless it is narratively earned and fragile.

The ocean and machines should carry more fear than score.

## 7. UI Audio

UI audio is instrument feedback:

- toggle click;
- relay thunk;
- archive verify chirp;
- pressure warning pulse;
- corrupted read crackle;
- route plot tick;
- disabled command dead click.

No clean mobile-app sounds. No decorative menu whooshes unless tied to a physical carrier.

## 8. Performance And Implementation

Audio systems must obey:

- no managed allocation in hot paths;
- pooled events and voices;
- data-driven cue IDs;
- priority and virtualization;
- SPSC/ring buffers where mandated;
- low-cadence environmental parameter updates;
- mix snapshots tied to gameplay state;
- no string cue lookup in runtime hot paths.

High-end may add richer convolution, layers, or detail, but compact must keep route, warning, and threat information.

## 9. Audio QA Gates

Reject if:

- cue has no gameplay information;
- monster sound is generic;
- music hides system audio;
- warnings spam;
- UI audio feels like app chrome;
- no low-tier mix exists;
- hot path allocates;
- cue lookup uses strings;
- silence is never used;
- player cannot infer anything from the sound.
