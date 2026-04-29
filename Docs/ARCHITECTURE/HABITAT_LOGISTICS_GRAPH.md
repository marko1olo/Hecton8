# Habitat Logistics Graph

Date: `2026-04-29`  
Status: `PENDING VERIFICATION`

Purpose: canonical architecture contract for habitat logistics links, Bishop-frame pipe rendering, rupture buckling, and CSR adjacency rebuilds.

Supersedes: transient construction notes spread across dated audit bundles. Older one-shot construction writeups remain historical material, not architecture authority.

## Scope

- habitat module adjacency
- logistics pipe visual generation
- rupture propagation and visual aftermath
- support-loss cascade behavior

Out of scope:

- per-tick resource flow solve implementation details beyond the graph contract
- module authoring UX
- editor bootstrap tooling

## Owners

- `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs`
  - authoritative habitat topology snapshot
  - CSR adjacency build
  - support-loss rupture detection
  - logical edge severing on unsupported spans
- `Assets/_Project/Scripts/Core/LogisticsPipeBuilder.cs`
  - spline descriptor math
  - Bishop / rotation-minimizing frame transport
  - visual LOD selection
- `Assets/_Project/Scripts/Core/ConnectionSplineBatchRenderer.cs`
  - batched runtime mesh build for pipe proxies
  - Burst jobs for sample frames, tube vertices, and indices
  - line fallback for distant pipes
- `Assets/_Project/Scripts/Construction/BaseDegradationSystem.cs`
  - rupture state bridge from graph flags into visuals and downstream VFX/decal owners
- `Assets/_Project/Scripts/Construction/StructuralIntegrityProfile.cs`
  - authored rupture thresholds and decal atlas indices per material variant

## Graph Contract

Habitat topology is stored as a CSR snapshot:

```csharp
NativeArray<LogisticsNetworkGraph.LogisticsNode> Nodes;
NativeArray<int> EdgeOffsets;      // len = NodeCount + 1
NativeArray<int> EdgeDestinations; // contiguous adjacency
NativeArray<float> EdgeResistance; // aligned with EdgeDestinations
```

Mutation source is module/socket discovery. Runtime traversal reads CSR only.

Rebuild sequence:

1. Clear previous visual links.
2. Re-index placed modules into `ModuleRecord`.
3. Quantize sockets and assemble provisional undirected edges.
4. Build node records.
5. Build edge records, evaluate unsupported spans, and mark ruptures.
6. Publish degradation state.
7. Publish the logical `LogisticsNetworkGraph`.
8. Publish visual spline links.

Unsupported span rule:

- if link length `> 15m`
- and no intermediate support module is inferred near the segment
- then both endpoint nodes inherit `LogisticsNodeFlags.Ruptured`
- and the edge is marked `Severed`
- and the edge is excluded from CSR publication

Result: structural breakage visually persists, but the power / atmosphere graph splits into isolated islands immediately on rebuild.

## Bishop Frame Spline Contract

Pipe visuals use a cubic Bezier derived from the two socket positions and their forward vectors.

Control point resolution:

```csharp
p0 = start;
p1 = start + startForward * handleLength;
p2 = end   - endForward   * handleLength;
p3 = end;
```

Sampling uses a Bishop / rotation-minimizing frame, not Frenet frames. That avoids flips at zero-curvature intervals and at hard direction changes.

Frame propagation:

```csharp
center  = EvaluateSpline(p0, p1, p2, p3, t);
tangent = normalize(EvaluateTangent(p0, p1, p2, p3, t));

if first sample:
    ResolveInitialFrame(tangent, out normal, out binormal);
else:
    TransportFrame(prevTangent, tangent, prevNormal, prevBinormal, out normal, out binormal);
```

The renderer samples `8` spline points per link and extrudes either:

- `8` radial segments for near tubes
- `4` radial segments for mid-distance tubes
- `2` vertices plus `MeshTopology.Lines` for far links

LOD thresholds:

- `< 40m`: `Tube8`
- `>= 40m` and `< 100m`: `Tube4`
- `>= 100m`: `Line`

## Rupture Buckling Contract

Rupture is a visual modifier layered onto the existing spline tube build. It does not own flow math.

Inside `BuildTubeVerticesJob`:

```csharp
float3 position = center + radialDirection * Radius;

if (HasRupturedMask(descriptor.Flags))
{
    float ruptureHash = ResolveRuptureHash(linkIndex);
    position += radialDirection * math.sin(position.z * 15f + ruptureHash) * 0.15f;
}
```

This executes inside Burst on native arrays only. No managed allocations, no `SetVertices`, no mesh CPU copies through managed containers.

## Mesh Upload Contract

`ConnectionSplineBatchRenderer` uploads generated geometry through `Mesh.AllocateWritableMeshData` and `UnsafeUtility.MemCpy`.

```csharp
void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(source);
void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(destination);
UnsafeUtility.MemCpy(destinationPtr, sourcePtr, (long)count * UnsafeUtility.SizeOf<T>());
```

Rules:

- no managed vertex arrays
- no `Mesh.SetVertices`
- no `mesh.vertices` copies
- no `CopyFrom` staging on hot path

## Rupture Aftermath Contract

`BaseDegradationSystem` is cold-path only. It is entered during habitat graph rebuild, not polled every frame.

On node transition into `Ruptured`:

1. Compute rupture world position from a ruptured edge or module origin.
2. Convert that point to AUP via `HectonFloatingOrigin`.
3. Mark connected pipe links ruptured in `ConnectionSplineBatchRenderer`.
4. Dispatch leak VFX through `BaseModule.EmitHullBreachJet(...)`.
5. Dispatch fluid aftermath through `AbyssalFluidDecalManager.RegisterRuptureFluid(...)`.
6. Rebuild the crack decal matrix cache for downstream decal consumers.

Current limitation:

- there is no existing first-party global crack-decal renderer owner in this slice
- the canonical output is therefore:
  - existing leak VFX
  - existing fluid decal owner
  - exposed crack-decal matrix cache for future renderer consumption

## Structural Integrity Profile

`StructuralIntegrityProfile` defines per-material rupture authoring:

- `MaxUnsupportedSpan`
- `BaseHP`
- `RuptureDecalAtlasIndex`

Current material variants:

- Glass
- Titanium
- Plasteel

## Failure Modes

- socket forward authoring can still produce ugly handles if prefab sockets are misoriented
- rupture buckling uses `position.z` phase, so world orientation affects crush wave pattern
- support inference is proximity-based, not explicit “support pylon” taxonomy
- if rebuild cadence lags, visual rupture onset lags with it
- if no downstream crack renderer binds the matrix cache, only authored decals / fluid aftermath appear

## Verification Checklist

- Unity compile: whole-project console must be `0` errors
- touched scripts validate individually
- far-pipe LOD falls back to `MeshTopology.Lines`
- unsupported span rebuild removes severed edges from CSR
- ruptured links show visible radial buckling
- rupture event emits leak jet and fluid decal without managed allocations in hot path
