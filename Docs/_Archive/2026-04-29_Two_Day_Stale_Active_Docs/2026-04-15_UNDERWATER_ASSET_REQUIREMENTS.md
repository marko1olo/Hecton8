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
- module wetness around leak points is now being authored with existing `Assets/ScifiFacility/Materials/GlassWet.mat` on small `LeakWetSheen` quads instead of requesting a dedicated wet-streak texture set in the first pass.
- composite ruin seep wetness is now being authored with existing `Assets/ScifiFacility/Materials/GlassWet.mat` on small `RuinSeepSheen_*` quads instead of requesting a dedicated mineral-wet decal texture set in the first pass.
- ruin-local `micro-life silhouettes` are now being authored by reusing existing `Mat_Support_CreaturePassive.mat` and `Mat_Support_CreaturePredator.mat` on a few small primitive forms inside ruin `LOD0/LOD1`, instead of introducing a separate fish texture set or scene-global swarm solution in the first pass.
- hazard-pocket vent shimmer is now being authored by reusing existing `Assets/ScifiFacility/Materials/GlassWet.mat` on small `VentSheen_*` quads under `LOD0/LOD1`, instead of requesting a dedicated vent-distortion or toxic-shimmer material in the first pass.
- safe/hazard support-pocket fauna hints are now being authored by reusing existing `Mat_Support_CreaturePassive.mat` and `Mat_Support_CreaturePredator.mat` on a few small primitive forms inside pocket `LOD0/LOD1`, instead of requesting dedicated fish atlases or a separate pocket-fauna system in the first pass.
- resource-pocket fauna hints are now also being authored by reusing existing `Mat_Support_CreaturePassive.mat` on a few small primitive forms inside `PFB_Support_Pocket_Resource` `LOD0/LOD1`, instead of introducing a separate resource-node fauna material pass in the first pass.
- `ReefApex` support-zone fauna hints are now also being authored by reusing existing `Mat_Support_CreaturePassive.mat` on a few small primitive forms inside `PFB_Support_Zone_ReefApex` `LOD0/LOD1`, instead of introducing a separate support-zone fauna atlas or scene-global reef-swim system in the first pass.
- `LargeThreat` support-zone predator hints are now also being authored by reusing existing `Mat_Support_CreaturePredator.mat` on a few small primitive forms inside `PFB_Support_Zone_LargeThreat` `LOD0/LOD1`, instead of introducing a separate apex-zone predator atlas or runtime stalker system in the first pass.
- `AbyssApex` and `RuinApex` support-zone predator hints are now also being authored by reusing existing `Mat_Support_CreaturePredator.mat` on a few small primitive forms inside their `LOD0/LOD1` groups, instead of introducing dedicated apex-fauna atlases or any scene-global predator runtime layer in the first pass.
- tiny underwater fauna-hint renderers are now treated as `no-shadow` authored forms in the first pass, so the current procedural silhouettes do not force a dedicated tiny-fauna shadow treatment or extra dark-accent texture just to justify their presence.
- support-creature spawn finals are now also being authored with a few tiny `fry / scout` hint forms in `LOD0/LOD1`, reusing the same support-creature materials and `no-shadow` policy instead of requesting separate spawn-fauna atlases in the first pass.
- debris finals are now also being authored with a few tiny scavenger hint forms plus a real `LOD1`, reusing the same support-creature materials and `no-shadow` policy instead of requesting separate debris-fauna atlases in the first pass.
- selective industrial underwater decals are now being authored by reusing existing `Assets/ScifiFacility/Prefabs/decals/*` stripe/scuff prefabs on module and ruin `LOD0` surfaces, instead of requesting a dedicated underwater decal atlas or scene-global projector layer in the first pass.
- visor imperfection fallback is now partially procedural inside `NASAPunk/SuitVisor`, because `Mat_Visor_Glass` currently has valid runoff textures but no authored `_ScratchNormalMap` or `_FingerprintTex`; this avoids treating those missing slots as a mandatory blocker in the first pass.

Reason:

- avoids adding memory cost before the procedural version is judged in runtime;
- keeps the first pass cheap on MX350-class hardware;
- prevents fake asset churn before visual proof exists.

## Pending Asset Requests

None yet.

## Verified Authoring Gaps

- `Assets/_Project/MasterMixer.mixer` currently contains only one snapshot named `Snapshot`.
- There are no authored `Underwater / BaseInterior / Surface / SurfaceRain / SurfaceStorm` mixer snapshots in the project at this time.
- Result: `AcousticZoneController` now compiles and resolves `masterMixer`, but true acoustic zone differentiation is still blocked by missing authored audio data, not by code.
- This is a verified asset-authoring gap, not an inferred one.
- `Assets/_Project/MasterMixer.mixer` currently exposes no authored acoustic processing beyond `Attenuation`; there is no verified LPF / reverb-style effect graph on the mixer at this time.
- Result: even if named snapshots are authored later, the current mixer content still lacks the processing graph needed for real underwater/interior contrast until those effects are added.
- `Assets/_Project/Art/Materials/Mat_Visor_Glass.mat` currently has `_WaterDropletMaskTex` and `_WaterRunoffNormalTex` assigned, but `_ScratchNormalMap` and `_FingerprintTex` are still unassigned.
- Result: visor grime/scratch breakup is now backed by a procedural shader fallback, but authored scratch/smudge textures are still an optional future quality upgrade, not a closed authoring path.

## Candidate Future Requests

Add only if procedural pass is not enough after runtime review:

- dedicated `visor droplet mask` texture with controlled droplet breakup;
- dedicated `visor runoff normal` for better refractive streaking;
- optional `surface smear / salt residue` mask for repeated water exposure;
- optional `bubble vent atlas` if localized bubble fields need more authored shape variety.
- optional `visor scratch normal` and `visor smudge / fingerprint mask` if the new procedural visor fallback is not rich enough under final runtime review.
- optional `soft plume noise / foam mask` if the first-pass hazard-pocket vent columns read too much like generic particles under runtime lighting.
- optional `module leak bubble atlas / crack mask` if `LeakVfx` on damaged modules still reads too much like generic suspended dust after runtime review.
- optional `ruin seep bubble atlas / mineral stain mask` if the new ruin-local plumes need more authored silhouette breakup or wet mineral deposits on the surrounding geometry after runtime review.
- optional `wet streak breakup mask / refractive stain normal` if `LeakWetSheen` still reads too clean or too quad-like after runtime review.
- optional `ruin mineral crust mask / seep breakup normal` if `RuinSeepSheen_*` still reads too much like clean transparent quads after runtime review.
- optional `micro-life silhouette atlas / soft emissive accent mask` if the reused support-creature materials read too synthetic on ruin-local fauna silhouettes after runtime review.
- optional `vent shimmer breakup mask / thermal distortion normal` if `VentSheen_*` still reads too much like flat wet quads after runtime review.
- optional `pocket-fauna silhouette atlas / biolum accent mask` if the reused support-creature materials are not enough to sell safe/hazard pocket life in runtime review.
- optional `resource-forager silhouette atlas / mineral-glint accent mask` if the reused passive material is not enough to sell life around resource pockets in runtime review.
- optional `reef-drifter silhouette atlas / soft translucency accent mask` if the reused passive material is not enough to sell passive movement around large reef support zones in runtime review.
- optional `apex-sentry silhouette atlas / threat-glint accent mask` if the reused predator material is not enough to sell localized danger presence around large threat support zones in runtime review.
- optional `abyss-watcher silhouette atlas / deep-glint accent mask` if the reused predator material is not enough to sell near-monolith danger presence in `AbyssApex` runtime review.
- optional `ruin-sentinel silhouette atlas / scavenger-glint accent mask` if the reused predator material is not enough to sell perched life around `RuinApex` structures in runtime review.
- optional `spawn-fry silhouette atlas / subtle beacon-accent mask` if the reused support-creature materials are not enough to sell ambient life around passive/predator spawn finals in runtime review.
- optional `debris-scavenger silhouette atlas / rust-accent mask` if the reused support-creature materials are not enough to sell life around debris clusters and wreck fields in runtime review.

Do not treat these as approved tasks. They are placeholders until runtime proves the current procedural path is insufficient.
