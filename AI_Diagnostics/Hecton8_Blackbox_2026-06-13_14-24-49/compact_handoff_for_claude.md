# H8 Blackbox: AI Handoff
## Measured Facts
- Bootstrap State: `DIRECT_WORLD_START_DETECTED`
- Registry Phase: `Uninitialized`
- MapMagic Graph: `False`
- MapMagic Active Terrains: `0`
- Crest OceanRenderer Enabled: `False`
- URP Asset: `URP_Medium (PC_RPAsset)`
- Main Camera Found: `False`
- Console Errors: `0`

## Critical Findings
- [BOOTSTRAP_NOT_STARTED] Bootstrap has not started — registry phase is 0 and this is not the bootstrap scene: registryPhase=0, scene=
- [GLOBAL_REGISTRY_EMPTY_OR_UNREADY] GlobalRegistry phase is 0 and all service slots are null: phase=0, all slots null
- [NO_ACTIVE_CAMERA] No cameras found in the scene at all: 0 cameras

Given these measured facts, identify likely root cause and minimal next fix.
