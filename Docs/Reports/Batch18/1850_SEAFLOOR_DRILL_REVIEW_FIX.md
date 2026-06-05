# 1850 Seafloor Drill Review Fix

Status: STATIC PATCH ONLY

## Review Intake

Read-only reviewer found:
- likely compile error: `SeafloorDrillTool` used `AbsoluteUniversePosition` without the `Hecton8.World` namespace;
- AUP route weakness: drill resolved hit/source positions from current runtime origin instead of the canonical player-pose snapshot route used by `PlayerTool`;
- missing held prefab/item assets for `Item_Tool_SeafloorDrill` remain a route-production blocker;
- frame-latent hit/query coupling is a broader `RequestPrimarySurfaceHit` route issue and was not refactored without Unity proof.

## Changes

- Changed `PlayerTool.TryResolveRuntimeAup` from `private` to `protected`.
- Removed the duplicate `SeafloorDrillTool.TryResolveRuntimeAup` fallback method.
- `SeafloorDrillTool` now resolves source/hit AUP through the canonical player pose snapshot method inherited from `PlayerTool`.

## Remaining Gate

No fake prefab or item asset was created. The missing production assets are still intentionally visible to validators:
- `Assets/_Project/Prefabs/Tools/Held/Tool_SeafloorDrill_Held.prefab`
- `Assets/_Project/Data/Items/Tools/Item_Tool_SeafloorDrill.asset`

Those must be authored as real production assets, not placeholder primitives.

## Verification

```powershell
git diff --check -- Assets/_Project/Scripts/PlayerTool.cs Assets/_Project/Scripts/SeafloorDrillTool.cs
```

Result: clean except Git CRLF normalization warnings.

Static brace/name check:

```text
PlayerTool.cs braces 0 TryResolveRuntimeAup 2
SeafloorDrillTool.cs braces 0 TryResolveRuntimeAup 2
```

Unity compile/build was not run because Unity/editor compiler processes were already active.
