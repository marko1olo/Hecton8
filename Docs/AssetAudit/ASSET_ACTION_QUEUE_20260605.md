# Asset Action Queue - 2026-06-05

Status: `PENDING_VERIFICATION`.
Evidence class: `STATIC_SOURCE`, `STATIC_DOC`, `STATIC_IMAGE_QA`, `AUDIO_WAVEFORM_QA`.

This queue converts the asset audit into work orders. It is not acceptance proof and it does not authorize raw YAML edits, Unity import during a bad process gate, Crest material wrappers, or proxy promotion.

CSV source: `Docs/AssetAudit/ASSET_ACTION_QUEUE_20260605.csv`.

Execution specs:

- Texture recipe source: `Docs/AssetAudit/TEXTURE_AUTHORING_RECIPES_20260605.md`.
- Audio remediation source: `Docs/AssetAudit/AUDIO_ROUTING_REMEDIATION_SPEC_20260605.md`.
- Audio row matrix: `Docs/Audio/audio_remediation_matrix_20260605.csv` and `Docs/AssetAudit/AUDIO_REMEDIATION_MATRIX_REVIEW_20260605.md`.

## P0

1. Water visual: `foam.png` is visually rejected but serialized-reachable through active world/ocean users. Replacement priority is high because the bad source can affect the visible surface/photic route.
2. Flora materials: four `WorldProceduralProxy` flora/coral/kelp materials are serialized in `02_HECTON_WORLD.unity`. Visible route proxy contamination blocks promotion.
3. Audio routing: Addressables settings/groups/entries are absent. `MusicDirectorConfig_Global.asset` mixer refs are statically non-null, but MusicDirector prefab AudioSource `OutputAudioMixerGroup` refs still require Unity/audio proof.
4. Audio lifecycle: current `Player.prefab` static scan has 24 direct P1 footstep/UI AudioClip refs; prior P0 ambient/splash direct refs are cleared in source but still need Unity prefab readback. Addressables/release ownership and zero-GC audio lifecycle are unproven.

## P1

1. Aegir: `TX_H8AegirGasGiantBakedDisc_1428.png` is prototype-only but serialized-reachable in the active world route.
2. Sky slots: `Mat_HectonSky` and cloud stack need Unity readback before any binding claim.
3. Terrain PBR: wet basalt/shell/sand sources need cleaned channel authoring; direct generated import is rejected.
4. UI sprite: `oxygen-tank.png` is a black silhouette/mask referenced by `Suit_HUD_Canvas.prefab`; current role ledger expects mask/linear sRGB false, but the source is sRGB true. Use `ui/OXYGEN.png`, classify/fix the mask route, or split dual roles explicitly.
5. Music cadence: loud long beds and repeated stingers need MusicDirector runtime gating and listening proof.

## P2

1. Texture import roles: streaming mips, normal/mask import type, and sRGB settings need Unity/API readback and fixes after the process gate is clean.
2. Scene-flow context: `01_ORBIT` is enabled in BuildSettings but root doctrine says it is not main handoff. Asset reports must separate orbit refs from documented main handoff refs until architecture owner clarifies.

## Gate

Last process gate before creating this queue: CPU `100`, active `Unity`, `Unity.ILPP.Runner`, and `dotnet`. Therefore no Unity, import, prefab mutation, build, or Play Mode proof was attempted.

## Next Owner Order

1. Unity material readback owner: P0 water foam and P0 proxy flora first.
2. Audio owner: Addressables absence, MusicDirector prefab mixer fallback notes, and direct prefab clip refs.
3. Texture authoring owner: P1 Aegir/cloud and terrain PBR packs.
4. Mesh/prefab owner: candidate-pool promotion only after material proof.

Final status: `PENDING_VERIFICATION`.
