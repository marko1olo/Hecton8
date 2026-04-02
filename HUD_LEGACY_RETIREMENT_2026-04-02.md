# HUD LEGACY RETIREMENT — 2026-04-02

## What was wrong

- The project already had a newer HUD stack, but `HectonSuitHUD` still existed as a dead fallback.
- That made the project lie about its real state:
  - old component still sat in prefabs
  - new presentation code still contained legacy references
  - scan markers still depended on a cache that lived inside the old HUD class

## What was done

- Confirmed the active HUD in the live scene is:
  - `SuitHUDV4CanvasOverlay`
  - `SuitHUDPresentationController`
  - `VisorHUDController`
  - `HectonSuitHUDExtensions`
- Removed `HectonSuitHUD` from first-party prefabs.
- Removed runtime code references to `HectonSuitHUD`.
- Moved the reusable integer-string cache into `HudNumericStringCache`.
- Deleted `Assets/_Project/Scripts/HectonSuitHUD.cs`.

## What this means in simple terms

- The project no longer pretends that the old HUD is still part of the real player pipeline.
- The new HUD stack is now the only real first-party path.
- We no longer carry a dead HUD just because one utility array happened to live inside it.

## What was verified

- Unity compiles without `Error` after retirement.
- A short `play -> stop` smoke completed without first-party HUD errors.
- Console only showed an unrelated TMP/package log plus existing third-party warnings.

## What remains open

- Some historical docs still mention `HectonSuitHUD` as if it were active.
- Those references are documentation cleanup, not a live runtime blocker.
