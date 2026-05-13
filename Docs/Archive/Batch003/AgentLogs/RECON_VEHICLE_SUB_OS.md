# RECON_VEHICLE_SUB_OS

Date: 2026-05-12
Status: `PENDING VERIFICATION`

## Command Evidence
Command: `rg -n "Canvas|m_Canvas|CanvasRenderer|GraphicRaycaster" Assets/_Project/Prefabs -g "*.prefab"`

## Findings
- `Assets/_Project/Prefabs/PFB_Submarine_Core.prefab`: no `Canvas`, `CanvasRenderer`, or `GraphicRaycaster` hits.
- `Assets/_Project/Prefabs/Suit_HUD_Canvas.prefab`: contains the expected suit HUD canvas; this is not the submarine cockpit prefab.
- `Assets/_Project/Prefabs/HUD_Internal.prefab`, `Player.prefab`, and tool prefabs contain serialized canvas references, not submarine cockpit 3D-space Canvas components.

## Decision
No Canvas removal from the submarine prefab was performed. The cockpit runtime is authored as mesh/material/render-texture presentation and analytical panel input only.
