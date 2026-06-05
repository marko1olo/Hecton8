# Rationale 1872 - Player Body Visual Source Package

Evidence class: STATIC_SOURCE + STATIC_DOC only.

## Decisions

- Classified active `Swim_*` body pieces and `Suit_Visor` as primitive visual blockers because `Player.prefab` points their `MeshFilter.m_Mesh` at Unity built-in primitive meshes.
- Classified `Suit_Diegetic_HUD_V4_Projection` as HUD-only, not a body replacement candidate. It is the only non-primitive mesh found inside `Player.prefab`.
- Classified `RuntimeVisualProof` player suit materials, visor textures, visor shaders, and survival/swim data assets as reusable support sources only. They do not satisfy the missing authored suit/body mesh requirement.
- Kept the replacement contract visual-only: root movement capsule, tool anchors, hand guides, attachment transforms, camera/HUD stack, survival truth, and AUP/GlobalDataVault routes must remain owned by their current systems.
- Treated `PlayerSwimBlockoutRig.SetDebugCubesVisible(false)` / `showDebugCubes=false` as temporary mitigation only. Hiding cubes preserves rig transforms but does not satisfy the visual source/package acceptance floor.
- Required continuous `GlobalQualityWeight` scaling across compact, middle, high, and ultra consequences. No binary low/ultra quality switch is acceptable.

## Non-Decisions

- No prefab edits.
- No source edits.
- No Unity validation.
- No runtime visual acceptance claim.
