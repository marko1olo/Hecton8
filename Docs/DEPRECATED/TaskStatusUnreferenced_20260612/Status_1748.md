# Status 1748 - Decal Scorch And Waterline Split Projector

State: PARTIAL STATIC PASS
Last update: 2026-06-03T23:04:44+04:00

Completed:
- Loaded root authority, route bibles, and 8 relevant mandate files.
- Audited visor trauma/decal runtime, renderer feature, internal flood waterline runtime, Uber post shader, laser cutter decal producer, and surface/salt signal availability.
- Set dynamic decal active pool constants to 16 low / 64 high.
- Replaced desktop internal waterline screen split with scene-depth world-Y reconstruction and fwidth antialiasing.
- Confirmed active visor trauma route is DataVault/RenderGraph based, not URP DecalProjector based.
- Confirmed touched visor route has no DecalProjector, Graphics.Blit, new Material, Instantiate, Resources.Load, GlobalRegistry.Get<, TryGetLatestCreated, or hidden Complete() matches.

Unresolved:
- Laser cutter fills LaserCutGlowDecalRequestDTO but finalization only publishes impact VFX and drain signals. No verified consumer forwards GlowDecalRequests into DynamicDecalVaultRuntime.
- Salt crust projection has no verified VehicleSurfacedSignal or salt material/profile handoff in the audited route. Existing salt is shader/mask domain, not this decal projector route.
- Unity shader compile, profiler timing, and screenshots not run. A dotnet process was already active, so no build was started.

Scale consequences:
- Low: 16 active scorch/impact decals, mobile waterline keeps the cheaper existing camera-ray fake.
- Middle: continuous GlobalQualityWeight increases processed/uploaded decal budget toward 64.
- High: 64 active decal ring with oldest-overwrite behavior and per-pixel desktop waterline mask.
- Ultra: same 64 truth cap to prevent decal spam; saved cost goes to existing shader/refraction fidelity, not extra hot allocations.
