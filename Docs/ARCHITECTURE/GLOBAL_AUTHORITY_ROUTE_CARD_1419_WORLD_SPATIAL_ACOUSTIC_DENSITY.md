# Global Authority Route Card: World Spatial Acoustic Density

Owner: Agent 1419 / Echelon 3 Ecosystem spatial hash.

Status: YELLOW, pending Unity runtime/profiler validation. Static source checks passed; final `dotnet build` was blocked by CPU/compiler gate.

## Fact

Acoustic transient density sampled from `WorldSpatialHashGrid` is one native fact owned by `GlobalDataVault`.

## Route

- BufferID: `WorldSpatialAcousticDensityMap = 1419042`
- SystemID: `WorldSpatialHash = 163`
- Owner: `WorldSpatialHashGrid`
- Producer: `BuildAcousticDensityMap(int currentFrame)`
- GPU/texture upload: `TryUploadAcousticDensityMap(Texture3D destination, int requestedSampleCount)`
- Consumers: player critical procedural audio, PDA map threat pings, Suit HUD fallback density.

## Locking

- Write lock acquire/release:
  - `Assets/_Project/Scripts/World/WorldSpatialHashGrid.cs:1382` -> `1413`
  - `Assets/_Project/Scripts/World/WorldSpatialHashGrid.cs:1804` -> `1818`
  - `Assets/_Project/Scripts/World/WorldSpatialHashGrid.cs:2125` -> `2148`
- Read route:
  - `Assets/_Project/Scripts/World/WorldSpatialHashGrid.cs:2167` via `TryReadOnlyHandle`.

## Constraints

- No managed acoustic density array remains in `WorldSpatialHashGrid`.
- No `SetData` route is used for the scoped ecosystem GPU upload audit.
- If the Vault read route fails, consumers receive `false/default` and degrade visually/audio-wise instead of blocking.
