# Status: CRAFTING_ASSEMBLY_PROGRAMMER

Prompt: Holographic Assembly
Domain: Crafting / Gameplay Assembly
Status: PENDING VERIFICATION

## Task Checklist

- [ ] 1. SINGLETON ERADICATION: Purge `CraftingManager.Instance`.
- [ ] 2. SIGNAL MIGRATION: Consume `CraftingStartedSignal` and `CraftingCompletedSignal`.
- [ ] 3. ASMDEF ISOLATION: `Hecton8.Gameplay.Crafting` -> Contracts.
- [ ] 4. DEAD CODE HUNT: Eradicate `ParticleSystem.Play()` from the old crafting sequence.
- [ ] 5. THE HOLOGRAPHIC SHADER: Write `Hecton_HologramAssembly.shader`.
- [ ] 6. THE CLIPPING PLANE: Add `_AssemblyHeightY` with fragment `clip(localY - _AssemblyHeightY)`.
- [ ] 7. THE BURN EDGE: Add hot-blue/white rim near the assembly plane.
- [ ] 8. PROGRESS LERP: Fabricator `SlowTick` lerps height from bounds bottom to top using `CraftingProgress01`.
- [ ] 9. MATERIAL SWAP: At progress 1.0, swap hologram to actual material.
- [ ] 10. WELDING AUDIO: Emit `ToolAcousticSignal(Welding)` while progress < 1.0.
- [ ] 11. INVENTORY COMMIT: Push `ItemAcquiredSignal` only after visual assembly reaches 1.0.
- [ ] 12. AUP SHIFT SAFETY: `_AssemblyHeightY` uses fabricator/local space.
- [ ] 13. MATH LOD: Low tier skips burn edge calculation.
- [ ] 14. ZERO-GC: Cached `MaterialPropertyBlock`; no `new Material()` cloning.
- [ ] 15. ABORT LOGIC: Power loss pauses height and pulses red.
- [ ] 16. TELEMETRY: Write `FabricatorActiveCount` to Blackbox.
- [ ] 17. EVENT BUS: Emit `PowerDrainSignal` proportional to assembly speed.
- [ ] 18. CROSS-DOMAIN AUDIT: UI progress bar reads exact same `CraftingProgress01`.
- [ ] 19. OMEGA COMPILE CHECK: Verify clipping/shadow logic; disable hologram shadows.

## Iteration Notes

- Iteration 0: Prompt extracted from `Docs/Tasks/CURRENT_BATCH.md`; status/rationale initialized. No code touched.
