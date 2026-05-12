# LOG_FAUNA_ECOSYSTEM

## 2026-05-11 - ECO_DIRECTOR / FAUNA_ECOSYSTEM

STATUS: PENDING VERIFICATION

What was wrong -> Migration ties could corner-stack biomass if equal food scores used deterministic neighbor order. The batch also required proof that ecosystem macro-simulation stays headless, cold-tick, zero-GC, AUP-bound, and not backed by distance-falloff panic math.

What was done -> Audited tasks 1-20 against `EcosystemDirector.cs`, `FaunaSpatialHashRegistry.cs`, `PersistentWorldRegistry.cs`, `ChemicalInfluenceGrid.cs`, and compile output. Fixed one E3 compile error in `PredatorCognitionDomain.cs` by forwarding the existing `useHighTierSmoothSteering` bool into the private predator evaluation path. Task 15 is marked BLOCKED BY DEPENDENCY because the 50-150m fauna BRG visual owner is not exposed.

AUP tie-breaker bitwise logic:

```csharp
return ((candidateCoord.x * 73856) + (candidateCoord.y * 19349)) & 3;
```

Sector bit-mix:

```csharp
uint mix = ((uint)sectorX * 73856093u) ^ ((uint)sectorZ * 19349663u);
```

Cinematic cheats used -> 5s FrostTick Lotka-Volterra instead of per-frame ecology; R8 food heatmap instead of runtime food loops; packed `byte ApexInSector` instead of distance panic falloff; squared scent grid instead of particle scent simulation; AUP whale-fall POI instead of live corpse scans; reciprocal quantization instead of divides; fixed 18-handle spatial cleanup instead of full sweeps; async cached `CapsulecastCommand` instead of synchronous spawn wall checks.

Exact microseconds saved, static estimates only -> tie bucket 1-2 us per FrostTick; bit-mix 3-6 us per 128-sector solve; R8 heatmap 20-35 us per 128-sector solve; Lotka Burst cold tick 80-140 us per 128 sectors vs managed loop; SoA hibernation 60-120 us per 128 records; scent squared checks 8-18 us per 64 samples; spatial cleanup avoids 0.05-0.12 ms spike; apex async wall gate avoids 0.08-0.20 ms stall on spawn-check frames.

Build health -> `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` still fails, but not in `EcosystemDirector.cs` and not in `PredatorCognitionDomain.cs` after the fix. Current blocker is E2 `VoxelDeltaProcessor.cs`.

Omega polish -> Static scans found no `math.sqrt`, `math.normalize`, `Vector3.Distance`, `foreach`, string interpolation, `string.Format`, or `.ToString()` hits in audited ecosystem/fauna hot files. No additional honest calculations were replaced during polish because the audited implementation already uses the required cinematic cheats. Unity Console/profiler proof is absent.
## 2026-05-11 - Tier1 Proxy Handoff Continuation

What was wrong:
- Task 15 still had a domain hole. `DataOnly` fauna suppressed GameObject presentation between 50m and 150m, but there was no fauna-owned fixed handoff for an instanced visual owner.
- Implementing BRG draw directly in `EcosystemDirector` or `FaunaBrain` would have crossed renderer ownership and fought parallel agents.

What was done:
- Added `Assets/_Project/Scripts/Fauna/FaunaTier1LodProxyRegistry.cs`.
- Added 64-byte `FaunaTier1LodProxyEntry`: AUP blit, instance UID, species ID, flags, heading octant, health byte, hunger byte, quality byte.
- Added fixed 512-slot registry with cold arrays only: entries, occupied bitmap, free-slot stack.
- Added O(1) `RegisterOrUpdate()` and `Unregister()` handles; added bounded `CopyActiveEntries(NativeArray<FaunaTier1LodProxyEntry>)` for a future visual/BRG owner.
- Wired `FaunaBrain` Tier1 lifecycle: register/update on `DataOnly`, refresh during slow Tier1 tick, unregister on FullSim/Hibernating/despawn/disable/destroy.
- Added local generated `Hecton8.Core.csproj` include so dotnet can see the new fauna file.

Cinematic Cheats used:
- Tier1 heading is an octant from sign/dominance checks, not atan/quaternion/normalized math.
- Distant Tier1 truth is a 64-byte proxy, not hydrated GameObject behavior.
- Health and hunger are bytes; flags are packed; AUP is copied as a 48-byte blit payload.

Exact Microseconds saved:
- Estimated 20-45 us per 128 Tier1 fauna once BRG/visual owner consumes the slab instead of keeping hydrated hidden GameObjects.
- Estimated 2-5 us per 128 Tier1 fauna by using octant heading instead of angle/normalization.
- Exact profiler data absent. STATUS remains PENDING VERIFICATION.

Compile evidence:
- `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies -v:minimal` initially caught missing `FaunaTier1LodProxyEntry`; fixed by adding the generated project include.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` now reports no fauna/ecosystem errors; current failures are outside domain in `SaveBinaryPayloadCodec.cs`, `SaveBinaryStorage.cs`, `HabitatGraphManager.cs`, and `ConstructionManager.cs`.
