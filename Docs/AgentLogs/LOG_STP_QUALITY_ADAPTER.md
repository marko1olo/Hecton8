# STP_QUALITY_ADAPTER Log

## 2026-05-16 - STP Dynamic Resolution Adapter

What was wrong:
- Dynamic-resolution policy was split between world runtime scale and thermal-only graphics logic.
- No registry-facing `IResolutionScalerService` existed for render-scale policy.
- UI/HUD render textures could multiply by 3D render scale, which would let STP blur text.
- Stress smoothing was managed/local and not a native DataVault handoff.
- Final compile validation is blocked by external core project/tether contract churn.

What was done:
- Moved the adapter into `Assets/_Project/Scripts/Graphics/Scalability/`.
- Added `IResolutionScalerService`, `ResolutionScaleState`, `BufferID.ResolutionScaleState`, and `SystemID.GraphicsScalability`.
- Registered `GlobalRegistry.ResolutionScaler` and kept the old `IDynamicResolutionRuntime` writer path as the render-scale sink.
- Added one-frame-latent Burst EWMA for `SystemStress01`.
- Stored current scale, target scale, stress, tier, STP intent, sharpen, and AUP lock state in persistent native state.
- Implemented low-tier 0.5 base scale and 0.35 emergency scale.
- Kept high/ultra base at 1.0 with STP intent active for AA.
- Emitted `ResolutionChangedSignal` only on render-scale movement above 5 percent.
- Drove `_SharpenIntensity` from render-scale deficit.
- Removed HUD RT multiplication by 3D dynamic resolution while preserving valid UI/diegetic `targetTexture` paths.
- Added 300-frame blackbox telemetry with `CurrentRenderScale` and `StpActive`, dumping to `Docs/AgentLogs/Dump_STP_QUALITY_ADAPTER.bin` on NaN.
- Locked scale changes for three frames on `AupShiftSignal`.
- Static motion-vector check found no silt/bubble motion-vector writers; project VFX hit was debris `ForceNoMotion`.

Cinematic cheats used:
- Pixel-count fake: low-tier internal render scale 0.5, emergency 0.35, reconstructed by STP instead of native resolution.
- Temporal stability cheat: EWMA stress and AUP lock avoid scale yo-yo/history smearing.
- Sharpen cheat: one scalar increases perceived detail instead of adding a compensation pass.

Exact microseconds saved:
- Not measured. Source estimates recorded in `Docs/Tasks/Status_STP_QUALITY_ADAPTER.md`: 0-2 us/frame per adapter task, with real pixel savings dependent on GPU/resolution. Low-tier 0.5 scale is 25 percent pixel area; 0.35 is roughly 12 percent pixel area before STP.

Validation:
- `dotnet build Hecton8.Core.csproj --no-restore` attempt 1 failed on unrelated AI/tether/visor compile wall.
- Attempt 2 failed on duplicate tether signal definitions.
- Attempt 3 with restore failed because `Hecton8.Core.csproj` references missing `Assets/_Project/Scripts/Physics/Tethers/Contracts/TetherSignalContracts.cs`.
- Final validation: BLOCKED BY DEPENDENCY. No STP adapter compiler errors were reached in the logged gates.

## 2026-05-16 - Escalation Polish / Data Sovereignty Pass

What was wrong:
- The first pass still had adapter-owned persistent `NativeArray` fields for scale fallback and blackbox telemetry.
- Sequential native layouts left room for platform padding ambiguity on ARM64/Quest.
- The EWMA completion path could force a main-thread sync at the start of Tick.

What was done:
- Evicted STP telemetry into `GlobalDataVault` as `BufferID.ResolutionScaleTelemetry`.
- Removed adapter-owned persistent native arrays and the fallback scale buffer; the adapter now borrows DataVault views only.
- Converted STP/thermal render-state structs to explicit `Pack=1` layouts: 64B `ResolutionScaleState`, 48B `DrsTelemetryEntry`, 24B `DynamicResolutionRuntimeSnapshot`, and 20B `HardwareThermalSnapshot`.
- Changed hot-path EWMA completion to non-blocking unless teardown or DataVault hotswap forces structural sync.
- Re-ran static scans: no private `NativeArray`, no direct `new NativeArray`, no `Update`/`LateUpdate`/`FixedUpdate`, no managed event/delegate path in `Graphics/Scalability`.

Cinematic cheats used:
- Same pixel-count fake remains: low-tier 0.5 scale, emergency 0.35, STP reconstruction.
- Same reactive sharpen scalar remains; no extra post pass was added.
- High/Ultra remains 1.0 scale with STP/DLAA intent so visual overkill is left to downstream volumetric/silt/hull/particle systems instead of this policy layer stealing bandwidth.

Exact microseconds saved:
- Not measured. Source estimate changed only in failure/stall risk: local native allocation ownership removed, and Tick no longer forces EWMA completion unless already finished. The per-frame adapter estimate remains source-only at roughly 0-2 us by task.

Validation:
- `git diff --check` returned no whitespace errors for the STP-touched files; repository-wide check only reported existing CRLF warnings.
- `dotnet build Hecton8.Core.csproj --no-restore` attempt 4 failed outside this domain: `SargassumMicroFaunaBoids.cs` missing `EnsureVaultBufferHandle`, and `VehicleDockingModule.cs` missing `CacheFluidRuntime`/`ResetDockingRuntimeCaches`.
- No STP adapter or STP contract compiler errors appeared before the external wall.
