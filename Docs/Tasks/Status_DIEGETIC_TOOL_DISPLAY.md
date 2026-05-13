# Status: DIEGETIC_TOOL_DISPLAY

Agent: UX_ENGINEER
Domain: ECHELON 8 - PRESENTATION & UX (Interaction and Perception)
Prompt: Zero-GC Tool Screens
Task Count: 19 (batch declares 19 titanium tasks; numbered list contains 18 plus recursive re-verification)
Status: PENDING VERIFICATION

## Hygiene
- [x] Prompt extracted from `Docs/Tasks/CURRENT_BATCH.md` with CLI regex by id `DIEGETIC_TOOL_DISPLAY`.
- [x] Existing status/rationale files checked: both missing, no stale-batch content detected.
- [x] Relevant mandates identified: `UI_Diegetic_Physical_Interfaces`, `UI_Data_Streaming_ZeroGC_Optimization`, `OPT_Zero_GC_Policy_AllocFree_Mandate`, `REND_URP_Graphics_HotPath_Optimization_HLOD`, `OPT_Performance_Budgets_FrameTime_VRAM_Limits`, `CORE_Tools_Equipment_Interaction_Raycast_Heat`, `MATH_Coordinate_Precision_AUP_FloatingOrigin`, `REND_VR_Stencil_Masking`.

## Tasks
- [ ] 1. SINGLETON ERADICATION: Purge `WeaponUIManager.Instance`.
- [ ] 2. SIGNAL MIGRATION: Consume `ToolStateChangedSignal`.
- [ ] 3. ASMDEF ISOLATION: `Hecton8.UI.Tools` -> Contracts.
- [ ] 4. DEAD CODE HUNT: Eradicate standard `Canvas` and `CanvasScaler` from the player HUD overlay.
- [ ] 5. THE TOOL CAMERA: secondary orthographic camera renders only `ToolUI` layer into shared 256x256 `RenderTexture`.
- [ ] 6. MATERIAL BINDING: assign RT to emissive channel of tool 3D screen material.
- [ ] 7. RENDERGRAPH OPTIMIZATION: UI camera executes only when equipped and visible.
- [ ] 8. SPAN FORMATTING: `ZeroGCFormatter.FastIntToChars` / span path for Ammo, Heat, Distance.
- [ ] 9. TMP UPDATE: `TMP_Text.SetCharArray()` only.
- [ ] 10. BAR GRAPHS: shader quad reads `_ToolHeat01`; no `Image.fillAmount`.
- [ ] 11. CRITICAL FLASH: shader-side RT color inversion if heat > 0.9.
- [ ] 12. AUP SHIFT SAFETY: local tool render path is immune to AUP shifts.
- [ ] 13. MATH LOD: Low Tier disables RT camera and routes data back to 2D visor HUD.
- [ ] 14. ZERO-GC: text formatting and UI updates allocate 0 bytes.
- [ ] 15. VRAM BUDGET: one shared 256x256 RT pool for active tool.
- [ ] 16. BLACKBOX DUMP: N/A recorded with justification.
- [ ] 17. VR COUPLING: readable tool mesh screen at arm's length in OpenXR.
- [ ] 18. OMEGA COMPILE CHECK: verify no `string.Format` exists.
- [ ] 19. Recursive re-verification: re-read prompt; ensure tool camera disabled when holstered and status remains PENDING VERIFICATION.
