# Status - DIEGETIC_DAMAGE_HOLOGRAPHER

Status authority: PENDING VERIFICATION until Unity compile, console, and scene evidence exist.
Domain: ECHELON 8 PRESENTATION & UX.
Prompt task count: 19.
Mandates read before coding:
- UI_Diegetic_Physical_Interfaces.txt
- UI_Data_Streaming_ZeroGC_Optimization.txt
- GPU_Compute_Kernels_Kernels_Optimization_MX350.txt
- REND_GPU_Sovereignty.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- CORE_Damage_System_Hull_Integrity_VFX_Feedback.txt

## Task Checklist

- [ ] 1. SINGLETON ERADICATION: Extend VehicleSubOsCockpitRuntime. DOD: pending code inspection. Rejected alternative: new singleton manager. Estimate: TBD us.
- [ ] 2. SIGNAL MIGRATION: Consume HullDeformedSignal. DOD: pending signal contract discovery. Rejected alternative: per-frame hull polling. Estimate: TBD us.
- [ ] 3. ASMDEF ISOLATION: Hecton8.UI.Diegetic -> Contracts. DOD: pending asmdef graph inspection. Rejected alternative: broad gameplay assembly dependency. Estimate: TBD us.
- [ ] 4. DEAD CODE HUNT: Eradicate 2D Canvas sprites used for Submarine Health UI. DOD: pending UI search. Rejected alternative: leaving duplicate health path alive. Estimate: TBD us.
- [ ] 5. THE HOLO-MESH: Use low-poly submarine proxy mesh LOD3. DOD: pending asset discovery/fallback contract. Rejected alternative: runtime mesh generation in hot path. Estimate: TBD us.
- [ ] 6. COMPUTE SHADER INJECTION: Create Hecton_DamageHologram.compute. DOD: kernel compiles and accepts proxy vertices plus _HectonHullDents[16]. Rejected alternative: CPU distance scan. Estimate: TBD us.
- [ ] 7. VERTEX DISTANCE CHECK: Compute squared distance to 16 dents. DOD: bounded 64-thread kernel with finite/radius guards. Rejected alternative: sqrt distance. Estimate: TBD us.
- [ ] 8. COLOR BUFFER APPEND: Append local coordinate + severity to StructuredBuffer<float4>. DOD: capped 512 points and zero radius skip. Rejected alternative: CPU readback. Estimate: TBD us.
- [ ] 9. POINT CLOUD DRAW: DrawMeshInstancedIndirect in VISUAL_SYNC. DOD: indirect args via CopyCount, no CPU readback. Rejected alternative: GameObject cubes. Estimate: TBD us.
- [ ] 10. IDLE SCANLINE: Cyan scanline when no damage. DOD: shader-side sine/triangle fake, no CPU instances. Rejected alternative: animated UI Canvas. Estimate: TBD us.
- [ ] 11. FLICKER ON HIT: Consume HighSpeedImpactSignal and flicker alpha for 0.5s. DOD: deterministic state timer, no coroutine. Rejected alternative: Animator/Coroutine. Estimate: TBD us.
- [ ] 12. ROOM FLOODING TIE-IN: Query RoomWaterLevels from Data Vault. DOD: decoupled interface or documented dependency block. Rejected alternative: hard direct dependency. Estimate: TBD us.
- [ ] 13. AUP SHIFT SAFETY: Local-space evaluation. DOD: no world/AUP dependency in compute path. Rejected alternative: world-space dent lookup. Estimate: TBD us.
- [ ] 14. MATH LOD: Low tier disables compute mapping and uses static warning icon. DOD: tier gate with MX350 fallback. Rejected alternative: always-on compute. Estimate: TBD us.
- [ ] 15. ZERO-GC: Persistent compute buffers, 0 bytes allocated. DOD: no hot-path new/LINQ/string/Find. Rejected alternative: dynamic lists/arrays. Estimate: TBD us.
- [ ] 16. VRAM BUDGET: Point cloud max 512 points. DOD: buffer capacity fixed and documented. Rejected alternative: unbounded append. Estimate: TBD us.
- [ ] 17. BLACKBOX DUMP: Push HoloDamagePoints to Telemetry. DOD: fixed 300-frame ring/dump hook or documented bridge. Rejected alternative: Debug.Log-only. Estimate: TBD us.
- [ ] 18. EXECUTION PHASE: Evaluated in VISUAL_SYNC. DOD: tied into existing visual sync/update phase. Rejected alternative: arbitrary Update path. Estimate: TBD us.
- [ ] 19. OMEGA COMPILE CHECK: Compute shader handles empty dent array. DOD: validation/compile and zero-radius branch. Rejected alternative: trusting shader defaults. Estimate: TBD us.

## Loop Ledger

- Loop 0: Prompt extracted and mandates read. Code inspection pending.
