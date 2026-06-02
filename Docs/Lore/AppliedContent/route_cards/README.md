# AppliedLore Route Cards

Status: checked authoring source.

Route cards are the gameplay-facing bridge between baked AppliedLore packets, discovery phases, replay variation, and ending pressure.

They are not runtime markdown. They are input for quest/PDA/POI planning and a future static-data route bake.

## Files

- `RS001_RS003_route_cards.csv`: first route-card layer for the 15 currently baked packets.

## Runtime Rule

- Every baked packet must appear in at least one route card.
- Route cards may group packets when the player-facing beat is a combined situation rather than a single prop.
- Runtime should consume future baked route IDs, packet hashes, phase hashes, depth bounds, surface masks, and prerequisite hashes.
- No runtime CSV parsing.
- No per-frame route graph walk.
- Unlocks still enter through `H8AppliedLoreRuntime.TryRaisePacketUnlocked`.

## Current Card Families

- `RC001_SURVIVE_DROP`: arrival survival, capsule damage, first compromised carrier contact.
- `RC002_PERSONAL_HOOK`: Barnard/frontier trace that turns contract work personal.
- `RC003_VALUE_TRAP`: blue debt, dead claims, and Black Keel accounting pressure.
- `RC004_REPAIR_SCAR`: Atlas category failure through repair scars.
- `RC005_PRESSURE_DESCENT`: brine/thermal route as hard-sci-fi traversal.
- `RC006_EVACUATION_CONTRADICTION`: Deep Reach public lie versus evacuation records.
- `RC007_HUMAN_SPACE_CONTEXT`: no-FTL logistics, domains, relays, ship classes, Aegir windows.
- `RC008_BOTTOM_FACTORY`: bottom factory as body/base/claim collision.
- `RC009_PAYLOAD_FALSE_EXIT`: payload mass, evidence, debt, route window, and partial endings.

## Verification

`Tools/AppliedLoreRuntimeAudit.py --root .` validates header shape, packet coverage, packet references, depth bounds, primary surfaces, and ending-pressure values.
