# SHINOBU_149 Dynamic Decal Status

Status authority: PENDING VERIFICATION until Unity import/shader compile/profiler proof exists.

## Loop 1 - Tasks 01-05

- [x] Task 01 DECAL_PROJECTOR_ERADICATION: Static scan found first-party runtime `DecalProjector` use in `SubmarineStructuralGrid`, inactive/active URP `DecalRendererFeature` renderer assets, legacy construction managed decal lists, and `AbyssalFluidDecalManager` mesh fallback. DOD practice: owner-local first-party scan before edits. Rejected alternative: preserve pooled projector because pooling still emits GameObject/component path. Estimate: removes 300-1400 us burst spikes under hull impacts plus batcher churn.
- [x] Task 02 SYNCHRONOUS_RAYCAST_PURGE: No new raycast path accepted; ballistics already provides `BallisticHitResultDTO.Normal`, and submarine impact code already has contact normal. DOD practice: consume hit normals from existing signal/result DTOs. Rejected alternative: re-query physics surface normal during decal placement. Estimate: avoids 40-250 us main-thread stall per clustered hit.
- [ ] Task 03 CS1612_ENCAPSULATION_PURGE: In progress. New `DecalInstanceDTO` uses raw fields and pointer jobs; no C# properties in hot DTO.
- [ ] Task 04 ARM64_PADDING_RECONSTRUCTION: In progress. New DTO will be explicit 80 bytes with editor/runtime layout validation.
- [ ] Task 05 EMERGENCY_MOCK_DECAL_INJECTION: In progress. New Burst mock request injector will write synthetic requests into the native queue.

## Loop Notes

- Relevant mandates read: Zero-GC, Native/Vault ownership, ARM64 struct layout, AUP precision, GPU sovereignty, MX350 compute budget, Cinematic Cheat Protocol, Black Box telemetry.
- Current implementation target: `Assets/_Project/Scripts/Visor/DynamicDecalVaultRuntime.cs`, `DeferredDecalPass.cs`, `Hecton_DeferredDecal.shader`, `SubmarineStructuralGrid.cs`.
