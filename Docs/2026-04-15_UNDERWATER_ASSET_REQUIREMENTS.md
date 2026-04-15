# HECTON-8 Underwater Asset Requirements Ledger

Status: `PENDING VERIFICATION`  
Date: `2026-04-15`

Purpose: separate ledger for underwater-specific texture / normal / mask / material authoring requests that may be needed later.

## Current Decision

No mandatory new textures are required for the current `wet visor / runoff` slice.

Current implementation direction:

- visor runoff is being implemented procedurally in the existing `NASAPunk/SuitVisor` shader;
- runtime control stays in `VisorHUDController` via `MaterialPropertyBlock`;
- underwater transitions stay owned by `HectonUnderwaterVisuals`.
- bottom-silt response is being implemented by boosting the existing `Underwater_SuspendedMotes` owner, not by introducing a second authored particle texture set.
- flashlight volumetric response is being implemented on the existing `DiveLamp_Light` owner with VLB components, not by adding new beam textures or a duplicate light rig.
- localized hazard-pocket bubble vents are being authored first on the existing `PFB_Support_Pocket_Hazard` final prefab, using prefab-local particle columns before any dedicated global underwater ambient system is introduced.
- player exhale bubbles are being authored on a dedicated `Player/Main Camera/Underwater_ExhaleBubbles` child, reusing the existing dust-particle source prefab instead of adding a separate bubble texture pass up front.
- damaged module leak plumes are being authored on existing `BaseModule` finals (`PFB_Module_Corridor`, `PFB_Module_Foundation`) by reusing `Assets/VolumetricLightBeam/Resources/DustParticles.prefab` instead of requesting a separate leak-particle atlas in the first pass.
- composite ruin seep plumes are being authored on `PFB_Ruin_ClusterMedium` and `PFB_Ruin_Megastructure` by reusing `Assets/VolumetricLightBeam/Resources/DustParticles.prefab` instead of introducing a separate ruin-bubble material set in the first pass.

Reason:

- avoids adding memory cost before the procedural version is judged in runtime;
- keeps the first pass cheap on MX350-class hardware;
- prevents fake asset churn before visual proof exists.

## Pending Asset Requests

None yet.

## Candidate Future Requests

Add only if procedural pass is not enough after runtime review:

- dedicated `visor droplet mask` texture with controlled droplet breakup;
- dedicated `visor runoff normal` for better refractive streaking;
- optional `surface smear / salt residue` mask for repeated water exposure;
- optional `bubble vent atlas` if localized bubble fields need more authored shape variety.
- optional `soft plume noise / foam mask` if the first-pass hazard-pocket vent columns read too much like generic particles under runtime lighting.
- optional `module leak bubble atlas / crack mask` if `LeakVfx` on damaged modules still reads too much like generic suspended dust after runtime review.
- optional `ruin seep bubble atlas / mineral stain mask` if the new ruin-local plumes need more authored silhouette breakup or wet mineral deposits on the surrounding geometry after runtime review.

Do not treat these as approved tasks. They are placeholders until runtime proves the current procedural path is insufficient.
