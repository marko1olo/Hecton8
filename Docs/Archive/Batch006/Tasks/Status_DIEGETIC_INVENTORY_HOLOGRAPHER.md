# Status_DIEGETIC_INVENTORY_HOLOGRAPHER

PROMPT: DIEGETIC_INVENTORY_HOLOGRAPHER
ROLE: UX_ENGINEER
DOMAIN: ECHELON 8 PRESENTATION & UX / Diegetic Inventory Hologram
TASK COUNT: 15
STATUS: PENDING VERIFICATION

Source prompt: `Docs/Tasks/CURRENT_BATCH.md` / `<AGENT_PROMPT id="DIEGETIC_INVENTORY_HOLOGRAPHER">`.
Mandates loaded: UI_Data_Streaming_ZeroGC_Optimization, UI_Diegetic_Physical_Interfaces, DATA_Inventory_Resources_Items_SOA_Layout, OPT_Zero_GC_Policy_AllocFree_Mandate, REND_GPU_Sovereignty, OPT_Performance_Budgets_FrameTime_VRAM_Limits, ARCH_Global_Registry_ServiceLocator_DI_Init, DBG_Telemetry_Crash_Reporting_PostMortem.

## Baseline Facts
- `InventoryChangedSignal` exists and is emitted by `PlayerInventory.NotifyInventoryChanged`.
- `PlayerInputSignal(ToggleInventory)` is missing and must be added as a typed native signal lane.
- `PlayerInventory` exposes SOA read-only `NativeArray` hashes/counts and `TryGetInventorySoA`.
- `InventoryUI.Instance` has no references in `Assets`; legacy `HectonInventoryUI` stub exists.
- No exact inventory prefab containing `ScrollRect` or `GridLayoutGroup` was found by repository scan.

## Checklist
- [ ] Task 1 - Singleton eradication | DOD pending: purge legacy inventory UI singleton/stub proof | Rejected pending | Estimate pending
- [ ] Task 2 - Signal migration | DOD pending: consume `InventoryChangedSignal` and `PlayerInputSignal(ToggleInventory)` | Rejected pending | Estimate pending
- [ ] Task 3 - ASMDEF isolation | DOD pending: `Hecton8.UI.Diegetic` depends on contracts/core only as required | Rejected pending | Estimate pending
- [ ] Task 4 - Dead code hunt | DOD pending: repository scan for inventory `ScrollRect`, `GridLayoutGroup`, `CanvasRenderer` | Rejected pending | Estimate pending
- [ ] Task 5 - Grid job | DOD pending: Burst grid job maps SOA inventory to camera/VR-local layout | Rejected pending | Estimate pending
- [ ] Task 6 - Matrices generation | DOD pending: persistent `NativeArray<float4x4>` up to 64 slots | Rejected pending | Estimate pending
- [ ] Task 7 - Icon atlas mapping | DOD pending: atlas UV offset encoded into matrix `m31/m32` | Rejected pending | Estimate pending
- [ ] Task 8 - Indirect draw | DOD pending: GPU double-buffer and `Graphics.RenderMeshIndirect` path | Rejected pending | Estimate pending
- [ ] Task 9 - Text projection | DOD pending: count labels via zero-GC char buffers/TMP `SetCharArray` | Rejected pending | Estimate pending
- [ ] Task 10 - VR interaction | DOD pending: right pointer hover expands matrix scale mathematically | Rejected pending | Estimate pending
- [ ] Task 11 - AUP shift safety | DOD pending: camera/local anchor math only, no world persistence | Rejected pending | Estimate pending
- [ ] Task 12 - Math LOD | DOD pending: low-tier flat projection, high-tier curved grid | Rejected pending | Estimate pending
- [ ] Task 13 - Execution phase | DOD pending: signal/job in update, draw in render lane | Rejected pending | Estimate pending
- [ ] Task 14 - Zero-GC | DOD pending: no runtime UI slots, no hot-path managed allocation | Rejected pending | Estimate pending
- [ ] Task 15 - Omega compile check | DOD pending: compile and shader UV decode proof | Rejected pending | Estimate pending

## Iteration Log
- Loop 0 / Baseline: prompt extracted with PowerShell regex from `CURRENT_BATCH.md`; neighboring prompts discarded. Mandates and domain docs read. No code modified yet.
