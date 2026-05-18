# Rationale_SHINOBU_68

## 2026-05-19 DRS Lane Reassertion

Problem: `CURRENT_BATCH.md` contains two `SHINOBU_68` XML blocks. The working status/rationale had drifted to the later procedural-bone duplicate even though the user's active request names DRS, TAA, post-processing, and URP Pipeline Assets.
Solution: Use the first CLI-extracted `SHINOBU_68` block at line 1524 as authority for this turn: `DYNAMIC_RESOLUTION_AND_FSR_DIRECTOR`, 20 task lines. Restore status/rationale to DRS-only.
Rejected Alternatives: Mixing procedural animation evidence into the DRS report or trusting chat memory.
Scalability potential: Low/MX350 and Quest survive through lower internal render scale and cheap bilinear+TAA. Middle uses steadier smoothing and conservative post. High/Ultra keep visual overkill feature weights and FSR/TAA reconstruction.
Hardware Impact: 0 us runtime; prevents wrong-domain edits and compile churn.

## 2026-05-19 TargetRenderScale And URP Asset Polish

Problem: Fixed display-resolution changes and abrupt render-scale jumps would reallocate buffers, damage VR comfort, and expose resolution loss.
Solution: Keep DRS inside URP/dynamic-resolution APIs. Target scale is `lerp(minScale, 1.0, GlobalQualityWeight)` plus stress collapse, thermal collapse, panic drop, and EWMA smoothing for current scale. PC URP assets use FSR override sharpness; mobile/Quest stays bilinear/TAA to avoid compute overhead.
Rejected Alternatives: `Screen.SetResolution`, `new RenderTexture`, binary low/high switches, and direct shader-specific dependencies.
Scalability potential: Low clamps near 0.6 and increases Dear Lie reconstruction. Middle smooths toward 0.7+. High/Ultra preserve visual overkill features and richer shader globals while staying continuous.
Hardware Impact: Avoids display-buffer reallocations; fill-rate saving is proportional to `1 - scale^2` once Unity runtime proof confirms active DRS.

## 2026-05-19 ARM64 DTO And Vault Audit

Problem: DRS state crosses DataVault, contracts, telemetry, and shader upload paths. Misaligned payloads would be hostile to Quest/ARM64.
Solution: `DrsStateDTO` remains 16B (`float,float,uint,uint`). `ResolutionScaleState` is 64B explicit layout. `DrsTelemetryEntry` is 48B explicit layout. `ValidateAbiLayout()` checks sizes before runtime activation. Persistent memory is requested through Vault handles, not private NativeArray fields.
Rejected Alternatives: `[StructLayout(Pack=1)]`, runtime bool fields in hot DTOs, private persistent NativeArray ownership.
Scalability potential: Stable layout lets low/mobile and high/desktop consume the same state with different visual budgets.
Hardware Impact: Avoids unaligned access traps and keeps hot state in compact cache-aligned rows.

## 2026-05-19 Heavy Post-Processing Survival Gate

Problem: At survival render scale, keeping SSDO, half-res transparent composition, and scooter volumetric shafts enqueued wastes RenderGraph setup and shader cost when the renderer is already fighting for frame time.
Solution: Add DRS survival gates to `HectonAbyssalSsdoFeature`, `HectonHalfResParticlesFeature`, and `HectonScooterVolumetricShaftsFeature`. Then remove duplicate per-feature `GlobalRegistry.ResolutionScaler` polling by centralizing the check in `HectonDrsRenderFeatureGate`, which caches the `IResolutionScalerService` contract and invalidates when state read fails.
Rejected Alternatives: Per-camera registry lookup in each feature, blanket disabling effects by hardware tier, or hard-coded renderer asset variants.
Scalability potential: Low/survival drops heavy post passes below 0.6001. Middle/High/Ultra keep passes available and are governed by global feature weights.
Hardware Impact: Reduces redundant service lookups after cache warmup and skips render-pass enqueue under survival pressure.

## 2026-05-19 Compile And Static Verification

Problem: Full project build would be noisy and expensive in a dirty 20-agent tree, but edited assemblies still need evidence.
Solution: Run scoped Roslyn csc only where needed. `Hecton8.Graphics.Scalability.rsp` passes. `Hecton8.Core.Contracts.rsp` plus `DrsContracts.cs` passes. `Hecton8.Core.rsp` with the new Visor helper reaches a pre-existing compile wall in unrelated construction/localization/netcode/geyser dependencies and emits no new DRS/Visor helper error.
Rejected Alternatives: Full `dotnet build`, ignoring compile evidence, or fixing unrelated owner domains.
Scalability potential: Static DRS lane remains decoupled; unresolved dependencies are outside DRS contract boundaries.
Hardware Impact: Developer machine protected; no full rebuild churn.
