# Rationale 1877

Evidence class: STATIC_SOURCE. Unity, build, import, PlayMode, profiler, screenshots, and asset generation were not run by task order.

## Decisions

- Implemented `ProductFacePlayerSuitMeshSourceAuthoring` as `#if UNITY_EDITOR` static authoring code under `Hecton8.Editor.ProductFace`.
- Kept the output route to future Mesh assets at `Assets/_Project/Art/Generated/ProductFace/PlayerSuit`.
- The script creates the output folder only if a future Unity owner executes the menu. It does not create materials, textures, prefabs, scenes, colliders, or runtime objects.
- `ValidateSourceAssumptions` fails closed if required material sources are missing because this task must not create materials.
- Used a static 10-entry source spec table matching the requested suit parts.
- Included mapping comments for future relink owners to connect generated visual sources to `Swim_*Attachment`, `HandAnchor`, `Suit_Visor`, and HUD projection roots without changing those owners.
- Used continuous `GlobalQualityWeight` to scale radial segments, hose segments, trim density, and bevel width. It does not change gameplay truth, collider ownership, DTO layout, save identity, camera, HUD, movement, or tool anchors.
- Mesh helpers build vertices and indices manually. No primitive GameObject route is used.

## Risk

- Compile remains PENDING VERIFICATION until Unity import/compile is allowed.
- Visual quality remains PENDING VERIFICATION until the future owner runs the generator, assigns materials, relinks prefabs, and captures screenshots.
- Folder creation inside the future menu execution still requires Unity/import proof. No folder was created in this task.
