# Habitat Logistics Graph

Date: `2026-04-30`  
Status: `PENDING VERIFICATION`

Purpose: canonical architecture contract for habitat logistics links, Bishop-frame pipe rendering, rupture buckling, and CSR adjacency rebuilds.

2026-05-03 current-state boundary:

- This is the habitat logistics architecture contract, not construction stress-test proof.
- Current project-state orientation starts at `Docs/Reports/2026-05-03_HABITAT_GRAPH_ANCHOR_STATE_HARDENING.md`, `Docs/Reports/2026-05-03_FOUNDATION_HARDENING_CONTINUATION.md`, and `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md`.
- `HabitatGraphManager`, `BaseModule`, `BaseAirlock`, `ConstructionManager`, and pipe rendering remain active review surfaces for native lifetime, save/load restoration, graph rebuild storms, and authority boundaries.

Supersedes: transient construction notes spread across dated audit bundles. Older one-shot construction writeups remain historical material, not architecture authority.

## Scope

- habitat module adjacency
- standardized module socket topology
- logistics pipe visual generation
- rupture propagation and visual aftermath
- support-loss cascade behavior
- construction graph persistence

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
  - connected-component low-power evaluation
- `Assets/_Project/Scripts/Core/LogisticsPipeBuilder.cs`
  - spline descriptor math
  - Bishop / rotation-minimizing frame transport
  - visual LOD selection
- `Assets/_Project/Scripts/Core/ConnectionSplineBatchRenderer.cs`

## Module Standard

- `Assets/_Project/Scripts/BaseModuleTemplate.cs` is the authored source of truth for module socket layout, proxy bounds, integrity defaults, and stable template hash IDs.
- `Assets/_Project/Scripts/ModuleSocket.cs` owns strict directional compatibility. A snap is valid only when the candidate socket direction is the inverse of the target socket direction and the compatibility lanes match.
- `Assets/_Project/Scripts/PlayerBuilder.cs` and `Assets/_Project/Scripts/Construction/ConstructionRuntimeProxyFactory.cs` must generate ghost/final proxy cubes when a buildable has no authored prefab. Proxy geometry is derived from `BaseModuleTemplate.proxyBounds*` and `socketDefinitions`.

## Persistence Contract

- `Assets/_Project/Scripts/SaveData.cs`
  - `ConstructionDTO.modules[]` remains the legacy/runtime-state channel for integrity, flooding, and component-specific payloads.
  - `ConstructionDTO.graphNodes[]` stores node AUP, module hash ID, prefab ID, and rotation.
  - `ConstructionDTO.graphEdges[]` stores undirected topology pairs using saved node indices.
- `Assets/_Project/Scripts/ConstructionManager.cs`
  - save writes both the flat module state and the graph topology in the same pass.
  - load prefers graph nodes for transform reconstruction and hash fallback, then restores runtime state from `modules[]`.
  - legacy saves without graph arrays still load through the flat module DTO path.
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
6. Traverse anchor reachability from all explicit anchor modules.
7. Publish unmoored state back into `BaseModule`.
8. Evaluate connected-component power state using traversal scratch, not anchor-state truth.
9. Publish emergency bulkhead lockdown state back into `BaseModule`.
10. Publish degradation state.
11. Publish the logical `LogisticsNetworkGraph`.
12. Publish visual spline links.

State separation rule:

- `_anchorReachability` is authoritative anchor/isolated-state truth after step 6.
- `_traversalVisited` is generic BFS scratch for component power, flood center-of-mass, and fungal target traversal.
- traversal scratch must never be read as anchored-state truth.

Unsupported span rule:

- if link length `> 15m`
- and no intermediate support module is inferred near the segment
- then both endpoint nodes inherit `LogisticsNodeFlags.Ruptured`
- and the edge is marked `Severed`
- and the edge is excluded from CSR publication

Result: structural breakage visually persists, but the power / atmosphere graph splits into isolated islands immediately on rebuild.

## Anchor Node Contract

Anchor state is explicit, not inferred from arbitrary support proximity.

- `Foundation` and `Pylon` modules are structural roots unless authoring overrides say otherwise.
- `BaseModuleTemplate.IsStructuralAnchor` is the primary authoring flag.
- `BaseModule.isStructuralAnchor` is the prefab/runtime fallback when a template is missing.
- legacy fallback IDs remain hard-coded only for known shipped content:
  - `Build_Foundation_Platform`
  - `Build_Utility_Pylon`

Traversal rule:

```csharp
seedQueue(allAnchorNodes);
while (queue not empty)
{
    node = pop();
    for each neighbor in csr(node)
        if not visited:
            visit(neighbor);
}
```

Any module not visited by that traversal is `UNMOORED`.

Unmoored consequences:

- the node inherits `LogisticsNodeFlags.Isolated`
- `LogisticsModuleStatusBits.Unmoored` is written into the node reserved byte
- `BaseModule.SetAnchoredState(false)` enables runtime buoyancy takeover for that module

If no anchor nodes exist in the current habitat island, the entire island becomes unmoored on the same rebuild.

## Synthetic Parasite Root Nodes

Parasite power drain is represented inside the habitat CSR snapshot as a synthetic module node. The parasite does not write directly into the power solver and does not mutate authored module templates.

Ownership:

- `Assets/_Project/Scripts/World/FloraInteractionManager.cs` owns root exposure, cutting, and parasite lifecycle state.
- `Assets/_Project/Scripts/BaseModule.cs` stores the host module's runtime infestation level and `RootPowerDrainWatts`.
- `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs` owns synthetic node injection during graph rebuild.

Injection sequence:

1. A parasite attaches to a concrete `BaseModule` and calls `SetParasiteInfestation(level, rootPowerDrainWatts)`.
2. The construction manager requests a habitat graph rebuild for the affected module.
3. `HabitatGraphManager.AppendParasiteRootNodes()` appends one synthetic `ModuleRecord` for each infected host with non-zero root drain.
4. The synthetic record stores `IsSyntheticParasiteRoot = true`, `HostModuleInstanceId`, and `SyntheticPowerDrainWatts`.
5. `BuildNodeRecords()` publishes that record into CSR with `CurrentLoad = -SyntheticPowerDrainWatts`.
6. Cutting the parasite clears the host infestation state, requests another CSR rebuild, and the synthetic node disappears on the next graph publication.

Sign convention:

```csharp
if (record.IsSyntheticParasiteRoot)
    node.CurrentLoad = -record.SyntheticPowerDrainWatts;
```

Negative load is a consumer drain. It is not generation and must not be offset before the logistics solver evaluates the island.

## Emergency Bulkhead Adjacency

Emergency airlock lockdown is driven from the same CSR snapshot.

- `BaseModuleTemplate.IsEmergencyAirlock` is the primary authoring flag.
- `BaseModule.isEmergencyAirlock` is the fallback.
- legacy airlock IDs remain hard-coded only for known shipped content:
  - `Build_Airlock_Hatch`
  - `base.module.airlock`

For each airlock node:

- scan all CSR neighbors
- if any adjacent module is currently breached
- push `SetEmergencyLockdown(true)` into the owned `BaseAirlock`
- mirror that state into `LogisticsModuleStatusBits.EmergencyLockdown`

This keeps structural anchor detection, rupture isolation, and emergency door response on one authoritative topology rebuild.

Manual override:

- quarantined `BaseAirlock` instances expose `Weld` and `PlasmaCut` interaction vulnerability masks
- continuous laser / weld heat accumulates for the authored override duration
- completion calls `SetEmergencyBulkheadLockdown(false)` and floods the protected module from the door point
- the breach point is reused as the flooding center-of-mass target and as the source of the transient depressurization vortex

## Hydro-Structural Flood Mass

Flooded rooms contribute physical mass to the graph:

```csharp
FloodMassKg = FloodLevel01 * VolumeM3 * 1025
DownwardLoadN = FloodMassKg * 9.81
```

`HabitatGraphManager.ApplyHydrodynamicStress()` performs two passes:

1. queue the local downward hydro load into each unmoored `BaseModule`
2. traverse each CSR island and compute the weighted flood centroid

Island centroid:

```csharp
weighted = sum(module.position * module.FloodMassKg)
centroid = weighted / sum(FloodMassKg)
```

The centroid is sent back to each unmoored module. `BaseModule.FixedTick()` then blends its Rigidbody `centerOfMass` toward that centroid, clamped by the authored maximum center-of-mass shift and per-tick solver limit. This makes flooded wings tilt toward the flooded mass instead of sinking as perfectly vertical blocks.

Hydro-shear rupture remains edge-local:

```csharp
if abs(FloodMassA - FloodMassB) > ShearThreshold:
    edge.Severed = true
    nodeA.Flags |= Ruptured
    nodeB.Flags |= Ruptured
```

## Catastrophic Implosion

Deep abandoned modules implode when:

```csharp
IntegrityState == Abandoned || IntegrityStateNormalized <= 0.4
ExternalDepthMeters >= 2000
```

Implosion is one-shot per module. Runtime effects:

- force the module flooded
- dispatch a `ForceMode.Impulse` packet through `PhysicsApplySystem`
- pull player, loose pickups, resources, scannables, and bioforms within `30m` toward the module center
- force the module unmoored
- mark the module as imploded
- rebuild the habitat graph

Unity joint destruction is intentionally not used. Project physics mandate forbids Unity joints, including `ConfigurableJoint`. The authoritative equivalent is CSR edge severing: during the next rebuild, any edge connected to an imploded module is marked ruptured and excluded from CSR publication.

## Pressure Buckling Stress

Deep-sea compression is mechanical, not visual-only. Each `BaseModule` publishes:

```csharp
CompressionAlpha01 = (1 - PressureCompressionAxisScale) / MaximumAxisLoss
```

During `HabitatGraphManager.ApplyHydrodynamicStress(dt)`, every non-severed CSR edge compares endpoint compression:

```csharp
deltaCompression = abs(CompressionAlphaA - CompressionAlphaB)
if deltaCompression > 0.15:
    overload01 = saturate((deltaCompression - 0.15) / 0.85)
    damage = overload01 * JointShearDamagePerSecond * dt * MaxIntegrity
```

Both endpoint modules receive the integrity damage. When normalized joint stress reaches `0.8`, `ProceduralAudioEvents.RaiseStructuralStressTriggered` publishes a zero-allocation structural groan event into the procedural audio renderer.

## Emergency Power Rerouting

Power routing uses the same logistics graph principles, but the owner is `PowerGrid` / `LogisticsNetworkGraph`.

Reroute rule:

- ruptured `PowerNode` endpoints do not publish power edges
- consumers on ruptured nodes are rejected by `LogisticsNetworkGraph.CanServeConsumer`
- the remaining CSR graph performs capped component traversal around the failed node if an alternate physical path exists
- if no alternate path exists, the consumer becomes isolated and brownout is applied

Looped networks are solved by conductance relaxation, not greedy propagation. Component demand is dispatched against component generation, then residual injection is cancelled at the component anchor so supply equals demand:

```csharp
CombinedResistance = EdgeResistance + SourceNode.Resistance + DestinationNode.Resistance
Conductance = 1 / CombinedResistance
Potential[i] = (sum(Conductance[i,j] * Potential[j]) + NetInjection[i]) / sum(Conductance[i,j])
Flow(i,j) = (Potential[i] - Potential[j]) / CombinedResistance
```

Jacobi relaxation is capped at `8` iterations with convergence cutoff `0.01`. BFS/component traversal is capped at `MAX_SEARCH_DEPTH = 100`; paths beyond that are treated as isolated to protect frame time. High-resistance alternate paths reduce potential and increase branch load. Overloaded nodes inherit `LogisticsNodeFlags.Overloaded`; consumer priority and component supply ratio then determine the brownout tier.

## Logic Spanner Bypass

The Logic Spanner inserts a temporary bypass edge between two placed modules. If the live CSR buffers have preallocated capacity, `HabitatGraphManager` inserts two directed edges directly into `EdgeDestinations` / `EdgeResistance` and updates offsets in place. If capacity is exhausted, the tool falls back to a full graph rebuild. Temporary bypass records are capped; runtime insertion never grows the backing `List<T>`.

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
