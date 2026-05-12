# RECON_UI_DIEGETIC_HUD

Status: PENDING VERIFICATION
Scope: `Assets/_Project/Scripts/UI/`

## Commands

- `rg -n 'Canvas\.ForceUpdateCanvases\(' Assets/_Project/Scripts/UI -g '*.cs'`
- `rg -n 'LayoutRebuilder' Assets/_Project/Scripts/UI -g '*.cs'`
- `rg -n '\.text\s*=' Assets/_Project/Scripts/UI -g '*.cs'`
- `rg -n 'HorizontalLayoutGroup|ContentSizeFitter' Assets/_Project/Scripts/UI -g '*.cs'`

## Findings

`Canvas.ForceUpdateCanvases()`:
- No matches.

`.text =`:
- No matches.

`HorizontalLayoutGroup` / `ContentSizeFitter`:
- No matches.

`LayoutRebuilder`:
- `Assets/_Project/Scripts/UI/UITooltip.cs:263` calls `LayoutRebuilder.MarkLayoutForRebuild(tooltipPanel);`
- `Assets/_Project/Scripts/UI/UITooltip.cs:270` calls `LayoutRebuilder.MarkLayoutForRebuild(tooltipPanel);`
- `Assets/_Project/Scripts/UI/PDAConstructionTab.cs:739` calls `LayoutRebuilder.MarkLayoutForRebuild(transform as RectTransform);`
- `Assets/_Project/Scripts/UI/LocalizedLayoutMirror.cs:206` calls `LayoutRebuilder.MarkLayoutForRebuild(rectTransform);`

## Risk

The recon violations are existing tooltip/PDA/localization layout rebuild paths. They were not edited in this UI_DIEGETIC_HUD pass because the prompt target is the diegetic visor lane and the compile wall is already outside this domain.
