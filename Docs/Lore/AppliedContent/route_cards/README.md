# AppliedLore Route Cards

Status: authoring source / route-card export candidate. Checked state requires a timestamped audit artifact.

Route cards are the gameplay-facing bridge between baked AppliedLore packets, discovery phases, replay variation, and ending pressure.

They are not runtime markdown. They are input for quest/PDA/POI planning and a future static-data route bake.

## Files

This README is not the current route-card inventory. Treat local route-card counts and family lists as static snapshots unless a timestamped command output or audit artifact says otherwise.

- `RS001_RS003_route_cards.csv`: historical first-wave route-card layer for the first 15 packets.
- `RS###*_route_cards.csv`: release-set route-card CSV pattern for later waves. The directory currently extends beyond RS001-RS003; use folder inventory and scoped audit output for current coverage.
- `drafts/`: packet/backlog route notes that are not current AppliedLore runtime source. Files here may keep legacy columns or future packet IDs; they are intentionally outside the `*_route_cards.csv` audit glob until converted to the runtime schema and backed by `applied_lore_packets.csv`.

## Runtime Rule

- Every baked packet must appear in at least one route card.
- Route cards may group packets when the player-facing beat is a combined situation rather than a single prop.
- Runtime should consume future baked route IDs, packet hashes, phase hashes, depth bounds, surface masks, and prerequisite hashes.
- No runtime CSV parsing.
- No per-frame route graph walk.
- Unlocks still enter through `H8AppliedLoreRuntime.TryRaisePacketUnlocked`.

## Historical First-Wave Card Families

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
