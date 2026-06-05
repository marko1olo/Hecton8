# Player UI Tool Product-Face Crosswalk - 2026-06-05

Status: `STATIC CROSSWALK / PENDING VERIFICATION`.
Evidence class: `STATIC_SOURCE + STATIC_DOC + STATIC_IMAGE_REJECTION`.

## Scope

This report maps runtime player/UI/movement blockers to product-face and h8_1475 proof blockers. It does not prove Unity state, Play Mode, profiler, GC, input readiness, visual acceptance, or runtime readiness.

CSV: `Docs/Reports/RuntimeSystem_20260605/PLAYER_UI_TOOL_PRODUCT_FACE_CROSSWALK_20260605.csv`.

Mandatory visual proof inputs:

- `Docs/Reports/Batch32/CONTROLLER_MANDATORY_VISUAL_REFERENCE_READ_20260605.md`
- `Docs/Reports/AssetSystem_20260605/H8_1475_VISUAL_REFERENCE_COMPARISON_TEMPLATE_20260605.md`

## What Was Wrong

- Current static scene evidence still allows a false proof path: active scene-local `Player` with `HectonWorldShellController1428` can drive camera movement while production `Player.prefab` remains unproven.
- HUD proof is split between `HUD_Internal`, `SuitHUDScreenCompositor`, and `SuitHUDV4CanvasOverlay`; `forceScreenSpaceOverlay: 1` remains a P0 proof blocker when interactive gameplay HUD is claimed.
- Product-face proof is not separate from runtime proof. A first-person survival route needs the active player, tool, HUD, prompt, movement, and visual surface/underwater route in the same proof packet.
- Latest direct image review rejects the foreground/tool silhouette as blockout-looking, so h8_1475 cannot pass with landscape-only shots.

## What This Crosswalk Does

- Creates 10 row-level blockers connecting player authority, movement/swim, input, HUD, interaction, foreground tool, player audio lifecycle, h8_1475 manifest, and visual floor.
- Names required readback fields before repair or acceptance.
- Names rejection conditions that stop h8_1475 or first-20 route promotion.
- Keeps all conclusions static and `PENDING VERIFICATION`.

## Current P0 Runtime/Product-Face Blockers

- Active player source must be read from Unity and reconciled with production `Player.prefab`.
- Shell direct input must not be the active control route.
- HUD must prove diegetic/projection/world-space or approved bridge behavior; interactive overlay shortcuts are rejected.
- Movement/swim proof must come from active `HectonPlayerMovement`, not shell transform movement.
- Foreground tool/product-face proof must appear in canonical screenshots.
- h8_1475 proof packet needs manifest, checksum, copied log, active player/HUD/tool fields, and `h8_1475_visual_reference_comparison.md` built from the current template.

## Low / Middle / High / Ultra Consequences

- Low: must preserve production player authority, readable HUD, foreground tool silhouette, and water/shoreline readability. Flat overlay and shell camera are rejected.
- Middle: must add interaction prompt, swim/walk transitions, cockpit/visor readability, and first-20 route object continuity.
- High: must buy stronger tool materials, HUD projection quality, shoreline contact, and Aegir/terrain integration.
- Ultra: can add richer screenshots and visual overkill only after runtime authority and product-face proof are already clean.

## Regression Model

- CPU: static report only; no runtime CPU claim.
- GC: static report only; no `0 B/frame` claim.
- Memory: no residency or Addressables claim.
- Cadence: future proof must show active owners in one route, not disconnected screenshots and stale static scans.
- Correctness: player/HUD/tool authority must be known before repair and before h8_1475 acceptance.
- Visual: latest MCP screenshots remain rejected diagnostic evidence.

Final status: `PENDING VERIFICATION`.
