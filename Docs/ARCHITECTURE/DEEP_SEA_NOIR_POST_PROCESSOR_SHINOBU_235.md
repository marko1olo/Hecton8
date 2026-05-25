# Deep Sea Noir Post Processor - SHINOBU_235

Status: PENDING VERIFICATION

Owner: Echelon 8 Presentation & UX / `Hecton8.Visor.HectonVisorUberPostFeature`.

Route card: `Docs/ARCHITECTURE/SHINOBU_235_DEEP_SEA_NOIR_ROUTE_CARD.md`, review disposition `YELLOW / STATIC_SOURCE_ONLY` until Unity import/profiler/player proof exists.

Route:

- `HectonVisorUberPostFeature` now defaults to a single RenderGraph fullscreen pass when `deepSeaNoirUnifiedPass` is true.

- CPU builds `NoirPostProcessInputDTO`, `NoirPostProcessTuningDTO`, and `NoirPostProcessDTO` from dispatcher `ILateFrameTickable.LateFrameTick`, not from `AddRenderPasses()`.
- DataVault lock/release owner tag is `SystemID.GraphicsScalability`.
- Reason: current `SystemID` enum has no Echelon 8 or Presentation rendering value.
- SHINOBU_235 owns route contract and proof artifacts.
- GraphicsScalability is the native-memory owner tag.

- Active-frame Vault access uses phase-local `TryResolveHandle` views only; no active Noir input/tuning/constants/telemetry lock/unlock pair is used because one-row mock and parameter math are direct owner-phase scalar methods, not tiny `IJob.Run()` calls.
- GPU upload uses double-buffered `GraphicsBuffer.Target.Constant` with `LockBufferForWrite` and `UnsafeUtility.MemCpy`; RenderGraph imports the active buffer with `ImportBuffer`, declares `UseBuffer(Read)`, and binds it through `SetGlobalConstantBuffer(buffer, nameID, offset, size)`.

- Shader: `Assets/_Project/Art/Shaders/Hecton_VisorGlitchACES.shader`.

- Vault IDs are centralized in `BufferID`: `Shinobu235NoirConstants/Input/Telemetry/Tuning/ColorProfiles/CsvScratch`.

- Active RenderGraph branch only checks readiness and enqueues against the previously published constant buffer.
- Dirty constant upload is owned by `LateFrameTick`.
- Vault handle creation, GraphicsBuffer creation, CSV load, and dependency refresh are cold/hot-swap responsibilities.
- `NoirPostProcessDTO` CBuffer lanes are fixed.
- `GrainParams`: intensity, scale, speed, wrapped time.
- `AberrationParams`: chroma intensity, X offset amplitude, Y offset amplitude, vignette.
- `ColorGrading`: contrast, saturation, temperature, depth tint.
- `QualityAndLimits`: quality, stress, toxicity, A/B split.

- CSV color profile selection is cached as one active `NoirColorProfileDTO` and refreshed on a continuous quality-scaled cadence from 18 frames at low quality to 2 frames at high quality.

- Player survival/movement references are cached from registry replacement.
- If player context initializes late, SHINOBU_235 retries through cached `IPlayerRuntimeContext`.
- Cadence: continuous `90..18` frames by `GlobalQualityWeight`.
- Active path does not poll `GlobalRegistry.Player`.
- Frame identity comes from `TimeSliceScheduler.CurrentFrameId`.
- Owner-local cold fallback exists.
- Visual grain/glitch phase advances from finite `SystemDispatcher.CurrentFrameDeltaTime`.
- Phase wraps at 1000 seconds.
- Active Noir path does not read Unity `Time.*`.
- Shared `HectonVisorUberPostFeature` uses dispatcher frame source for reconstruction telemetry and depthless-TBDR cache cadence.
- Previous concrete fluid runtime rebind was removed from this host.
- No touched active visor/noir route code uses `Time.frameCount`.
- The shared host player-context path no longer calls `PlayerRuntimeContextService.TryGetActiveRuntimeContext()` and no longer imports `Hecton8.Gameplay`; it uses the cached `IPlayerRuntimeContext` snapshot route already owned by the registry replacement phase.
- Shared host no longer imports `Hecton8.Physics`.
- It no longer samples `HectonFluidEngine.TrySampleMaelstromWarp`.
- Pressure/stress distortion stays a screen-space fake from existing pressure/hull-stress inputs until a contracts-only fluid read model exists.
- No active Noir one-row math uses Burst job scheduling or `.Run()`; the previous synchronous mock/parameter jobs were collapsed into direct methods to avoid scheduler overhead and false Burst-proof on non-batched work.
- `DeepSeaNoirTunerWindow` samples the editor graph into a fixed 128-float ring and updates its managed label only when quantized display values change.

- `Volume_Component_Inquisition` writes a scoped SHINOBU_235 report for `Assets/_Project/Prefabs`, `Assets/_Project/Scripts/Rendering`, and `Assets/_Project/Scripts/Visor`; scenes, URP assets, and UI settings Volume references are explicitly out-of-domain residue, not project-wide eradication proof.

Gameplay isolation:

- `NoirPostProcessInputDTO` is presentation-only and must not enter rollback hash or save identity.

- `GlobalQualityWeight` scales grain, glitch X/Y offsets, chroma, derived block scale, and detail math continuously.

- Shader fake:
  - one-hash procedural grain;
  - block glitch;
  - single-sample channel-phase chroma;
  - depth tint;
  - visor crack/edge shaping.
- URP Volume Tonemapping owns final ACES.
- Noir fragment pass stays pre-tonemap, preserves raw linear HDR above `1.0`, and does not `saturate(color)`.
- No physical simulation is owned here.
- Shader quality gates are arithmetic `step`/`lerp` masks, not hardware-tier branches or shader variants. The branchless path reads the camera color texture once.

- Noir reads player movement/survival through pure cached `IPlayerRuntimeContext` snapshot accessors. It does not keep direct `HectonSurvivalSystem` or `HectonPlayerMovement` references in the active input path.

Proof status:

- Static source route installed.

- Binary payload ledger row added in `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md` for Vault IDs `71040..71045`, DTO layout anchors, rollback exclusion, and Data Monolith non-readiness.

- Global authority route card added in `Docs/ARCHITECTURE/SHINOBU_235_DEEP_SEA_NOIR_ROUTE_CARD.md`.

- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` was attempted under CPU guard and failed before SHINOBU_235 code because `Assets/_Project/Scripts/Gameplay/HectonScannerProjectionState.cs` is deleted while still referenced by the generated project.

- Unity import, Console, Frame Debugger, RenderGraph capture, Profiler, GCMonitor, and player-build proof are absent.
