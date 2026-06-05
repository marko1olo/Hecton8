# Rationale 1748

Decisions:
- Capped DynamicDecalVaultRuntime.MaxCapacity at 64 and LowCapacity at 16 because the active pool itself must overwrite oldest on the 65th mark. Keeping a 128 backing store would not prove the requested active-count limit.
- Changed only the desktop Uber post waterline path. It already samples scene depth elsewhere, so reusing HectonFiniteSceneRawDepth, HectonSceneDepthValid01, and ComputeWorldSpacePosition is the smallest verified route.
- Kept the mobile waterline path as the existing cheap fake. The mandate allows cinematic fakes when they preserve the visual floor; mobile depth reconstruction would add cost to the wrong tier.
- Did not invent a laser-glow or salt-crust signal bridge. LaserCutterDodRuntime currently ignores the GlowDecalRequests buffer after scheduling, and no VehicleSurfacedSignal exists in the audited signal set.

Evidence:
- DynamicDecalVaultRuntime.cs:232-233 now defines 64/16 active pool constants.
- GenerateTraumaDecalMatricesJob writes at TotalWritten % Capacity and increments ActiveCount only up to Capacity, so the 65th accepted decal overwrites slot 0 under MaxCapacity 64.
- HectonVisorUberPost.shader:236-246 now reconstructs scene world position from depth, compares sceneWorld.y against _InternalWaterlineY, uses fwidth(yDelta), and falls back to the old screen split for invalid depth.
- LaserCutterDodJobs.cs:461 writes GlowDecalRequests; LaserCutterDodRuntime.cs:517 finalizes only PublishImpactSignals(impactVfx, count, presentationOriginAup).

Proof limits:
- Static proof only. Unity runtime visual/profiler evidence was not collected.
- dotnet build was not launched because a dotnet process was already running.
