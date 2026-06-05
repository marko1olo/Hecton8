# Rationale 2003

Static conclusion: dry placement risk is real because dry terrain above water is clamped to depth `0`, while multiple underwater ground-attached rules accept `minDepthMeters: 0`.

Primary decisions:

- Do not solve by deleting kelp/coral/rocks or reducing shallow ecology. Project bibles require premium surface/photic/shallow visuals.
- Require positive submerged depth floors for underwater ground-attached domains.
- Treat default/omitted substrate `Any` as invalid for strict underwater grounding unless the owner creates a distinct shoreline/intertidal route.
- Fix strict mapping so it enforces preferred biome/zone/socket filters instead of bypassing them.
- Block proxy-only variants on production-visible kelp/coral/rock/pocket scatter once final variants exist.
- Keep `GlobalQualityWeight` out of placement truth. It may scale density/fidelity/cadence, not dry/submerged authority.

No runtime proof was claimed. Owner must implement and validate in Unity.
