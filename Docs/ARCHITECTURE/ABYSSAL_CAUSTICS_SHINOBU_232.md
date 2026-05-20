# ABYSSAL CAUSTICS ROUTE CARD - SHINOBU_232

Owner: `SHINOBU_232`
Domain: `ABYSSAL_CAUSTICS_AND_PROJECTION_PASS`

## Authority Boundary

The system owns presentation-only caustic lighting parameters. It does not own sunlight, waves, cave topology, rollback state, or deterministic gameplay facts. External facts are read through cached registry and Vault routes, then collapsed into one 64-byte shader payload.

## Data Routes

- Input: `BufferID.ShinobuOceanWeatherState` when present.
- Input: `BufferID.ShinobuOceanWaveParameters` when present.
- Input: `BufferID.ShinobuOceanSurfaceSwell` when present.
- Output: `BufferID.ShinobuCausticsParameters` as one `CausticsParametersDTO`.
- Output: `BufferID.ShinobuCausticsTelemetryRing` as a 300-frame `CausticsTelemetryEntry` ring.
- Output: `BufferID.ShinobuCausticsTelemetryCursor` as one integer cursor.
- Tuning: `BufferID.ShinobuCausticsTuning`.
- CSV profiles: `BufferID.ShinobuCausticsProfiles`.
- CSV scratch: `BufferID.ShinobuCausticsCsvScratch`.

External weather, wave, and swell inputs are cached as non-owning `VaultGenerationHandle<T>` descriptors through `TryGetGenerationHandle`. The caustics runtime resolves them read-only per tick and never allocates, grows, or releases those producer-owned lanes.

Owner output/tuning/telemetry/profile lanes are cold-acquired once and guarded by `_vaultStateReady`. Per-frame `Tick` skips duplicate owner-lane acquire probes while generation descriptors remain valid. Failed required resolves, DataVault hot-swap, release, and shutdown clear the gate so the next frame reacquires through the Vault contract.

CSV profile names are cold-parsed with `ReadOnlySpan<byte>`. Known weather names map to canonical `WeatherState` masks so examples like `Calm` and `Hurricane` bind to `WeatherStateDTO.StateMask`; unknown names still produce FNV-1a keys for future biome/profile routes. Matched profiles feed scale, intensity, max depth, flow speed, chromatic dispersion, and SDF shadow strength into the 64-byte CBuffer. The default editable profile file is `Assets/_Project/Data/Rendering/caustic_lighting_profiles.csv`, exposed through the editor tuner reload button.

## Render Path

`HectonDeferredCausticsFeature` injects a URP RenderGraph full-screen pass. The shader reconstructs world position from the camera depth buffer, projects procedural Voronoi caustics mathematically, samples `_HectonCaveVoxelSdfTex` for cave attenuation, and composites into camera color. No Unity Projector, light cookie, caustic atlas RenderTexture, or per-object redraw is part of this route.

## Scalability

`GlobalQualityWeight` is consumed as a continuous scalar. Low quality contracts maximum caustic depth, collapses to one monochrome noise layer, and keeps cave shadowing to the first cheap SDF lookup. Middle quality blends the second caustic layer and admits partial sun-ray SDF samples. High and ultra quality add chromatic dispersion, deeper visibility, and the full four-sample SDF confidence path. The shader keeps the same route and changes mathematical budgets/weights, avoiding hardware class booleans.

The RenderGraph destination texture inherits the active camera color format and only strips depth, MSAA, mips, and auto-mips. This avoids fixed-format conversion risk while preserving the same fullscreen visual fake.

## Memory And Compile Guard

Runtime persistent CPU memory is Vault-owned. The runtime stores generation-handle descriptors and resolves phase-local `NativeArray` views only while writing or uploading. DTO layout is explicit and editor-audited through `UnsafeUtility.GetFieldOffset` in `AbyssalCausticsLayoutAudit`.

No direct dependency on sibling concrete rendering, physics, celestial, or voxel runtime types is required for execution. Optional external data is accepted through existing Vault IDs and the cached global services available at boot.
