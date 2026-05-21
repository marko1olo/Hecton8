# Deep Sea Noir Post Processor - SHINOBU_235

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-21 R51 Root/Architecture Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, shader import, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
Current DOC_GLOBAL boundary (2026-05-21 R51): `Docs/Reports/2026-05-21_DOCUMENTATION_R51_ROOT_ARCHITECTURE_ENCODING_BOUNDARY_READORDER_AND_ROUTE_GAPS_LOCAL.md` is the latest local static root/architecture encoding repair, boundary-gap, read-order, route-card/static-contract, and source/AtlasCheck orientation correction. R50 remains the prior generated-atlas regeneration, stale R48 interior-boundary, dump-target wording, and source-counter drift correction. R49 remains the prior AtlasCheck-red-state/boundary-gap/route-field/source-counter correction. R48 remains the prior date-rollover/AtlasCheck/source-counter correction. R47 remains the prior authority-spine/runtime-wording/counter-drift correction. R46 remains the prior interior-authority/route-field/proof-language correction. R45/R44/R43/R42/R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers. Current AtlasCheck remains red until `Tools/AtlasCheck.py` exits `0`; runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Status: PENDING VERIFICATION

Owner: Echelon 8 Presentation & UX / `Hecton8.Visor.HectonVisorUberPostFeature`.
Route card: `Docs/ARCHITECTURE/SHINOBU_235_DEEP_SEA_NOIR_ROUTE_CARD.md`, review disposition `YELLOW / STATIC_SOURCE_ONLY` until Unity import/profiler/player proof exists.

Route:
- `HectonVisorUberPostFeature` now defaults to a single RenderGraph fullscreen pass when `deepSeaNoirUnifiedPass` is true.
- CPU builds `NoirPostProcessInputDTO`, `NoirPostProcessTuningDTO`, and `NoirPostProcessDTO` from dispatcher `ILateFrameTickable.LateFrameTick`, not from `AddRenderPasses()`.
- DataVault lock/release owner tag is `SystemID.GraphicsScalability`, because the current `SystemID` enum has no Echelon 8 or Presentation rendering value. SHINOBU_235 owns the route contract and proof artifacts; GraphicsScalability is the native-memory owner tag for these GPU scalability lanes.
- Active-frame Vault access uses phase-local `TryResolveHandle` views only; no active Noir input/tuning/constants/telemetry lock/unlock pair is used because one-row mock and parameter math are direct owner-phase scalar methods, not tiny `IJob.Run()` calls.
- GPU upload uses double-buffered `GraphicsBuffer.Target.Constant` with `LockBufferForWrite` and `UnsafeUtility.MemCpy`; RenderGraph imports the active buffer with `ImportBuffer`, declares `UseBuffer(Read)`, and binds it through `SetGlobalConstantBuffer(buffer, nameID, offset, size)`.
- Shader: `Assets/_Project/Art/Shaders/Hecton_VisorGlitchACES.shader`.
- Vault IDs are centralized in `BufferID`: `Shinobu235NoirConstants/Input/Telemetry/Tuning/ColorProfiles/CsvScratch`.
- The active RenderGraph branch must only check readiness and enqueue the pass against the previously published constant buffer. Dirty constant upload is owned by `LateFrameTick`; Vault handle creation, GraphicsBuffer creation, CSV load, and dependency refresh are cold/hot-swap responsibilities.
- `NoirPostProcessDTO` CBuffer lanes are fixed: `GrainParams = intensity, scale, speed, wrapped time`; `AberrationParams = chroma intensity, X offset amplitude, Y offset amplitude, vignette`; `ColorGrading = contrast, saturation, temperature, depth tint`; `QualityAndLimits = quality, stress, toxicity, A/B split`.
- CSV color profile selection is cached as one active `NoirColorProfileDTO` and refreshed on a continuous quality-scaled cadence from 18 frames at low quality to 2 frames at high quality.
- Player survival/movement references are cached from the registry replacement phase. If the same player context initializes late, SHINOBU_235 retries through the cached `IPlayerRuntimeContext` on a continuous 90-to-18-frame `GlobalQualityWeight` cadence; the active path does not poll `GlobalRegistry.Player`.
- Frame identity comes from `TimeSliceScheduler.CurrentFrameId` with an owner-local cold fallback; visual grain/glitch phase advances from finite `SystemDispatcher.CurrentFrameDeltaTime` and wraps at 1000 seconds. The active Noir path does not read Unity `Time.*`.
- The shared `HectonVisorUberPostFeature` host file also uses the dispatcher frame source for reconstruction telemetry and depthless-TBDR cache cadence; the previous concrete fluid runtime rebind was removed from this host. No touched active visor/noir route code uses `Time.frameCount`.
- The shared host player-context path no longer calls `PlayerRuntimeContextService.TryGetActiveRuntimeContext()` and no longer imports `Hecton8.Gameplay`; it uses the cached `IPlayerRuntimeContext` snapshot route already owned by the registry replacement phase.
- The shared host also no longer imports `Hecton8.Physics` or samples `HectonFluidEngine.TrySampleMaelstromWarp`; pressure/stress distortion now remains a screen-space presentation fake based on existing pressure and hull-stress inputs until a contracts-only fluid read model is available.
- No active Noir one-row math uses Burst job scheduling or `.Run()`; the previous synchronous mock/parameter jobs were collapsed into direct methods to avoid scheduler overhead and false Burst-proof on non-batched work.
- `DeepSeaNoirTunerWindow` samples the editor graph into a fixed 128-float ring and updates its managed label only when quantized display values change.
- `Volume_Component_Inquisition` writes a scoped SHINOBU_235 report for `Assets/_Project/Prefabs`, `Assets/_Project/Scripts/Rendering`, and `Assets/_Project/Scripts/Visor`; scenes, URP assets, and UI settings Volume references are explicitly out-of-domain residue, not project-wide eradication proof.

Gameplay isolation:
- `NoirPostProcessInputDTO` is presentation-only and must not enter rollback hash or save identity.
- `GlobalQualityWeight` scales grain, glitch X/Y offsets, chroma, derived block scale, and detail math continuously.
- The shader fakes pressure/stress with one-hash procedural grain, block glitch, single-sample channel-phase chroma, depth-tint, and visor crack/edge shaping. URP Volume Tonemapping owns final ACES; the Noir fragment pass stays pre-tonemap, preserves raw linear HDR above 1.0, and does not clamp the color path with `saturate(color)`. No physical simulation is owned here.
- Shader quality gates are arithmetic `step`/`lerp` masks, not hardware-tier branches or shader variants. The branchless path reads the camera color texture once.
- Noir reads player movement/survival through pure cached `IPlayerRuntimeContext` snapshot accessors. It does not keep direct `HectonSurvivalSystem` or `HectonPlayerMovement` references in the active input path.

Proof status:
- Static source route installed.
- Binary payload ledger row added in `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md` for Vault IDs `71040..71045`, DTO layout anchors, rollback exclusion, and Data Monolith non-readiness.
- Global authority route card added in `Docs/ARCHITECTURE/SHINOBU_235_DEEP_SEA_NOIR_ROUTE_CARD.md`.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` was attempted under CPU guard and failed before SHINOBU_235 code because `Assets/_Project/Scripts/Gameplay/HectonScannerProjectionState.cs` is deleted while still referenced by the generated project.
- Unity import, Console, Frame Debugger, RenderGraph capture, Profiler, GCMonitor, and player-build proof are absent.
