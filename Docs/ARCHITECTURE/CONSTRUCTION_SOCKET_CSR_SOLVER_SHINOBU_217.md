# Construction Socket CSR Solver - SHINOBU_217

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-20 R45 Root/Architecture Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

Current root/architecture boundary is `Docs/Reports/2026-05-20_DOCUMENTATION_R45_ROOT_ARCHITECTURE_R43_R44_RESIDUE_PROOF_ARTIFACTS_AND_COUNTERS_LOCAL.md` as STATIC_DOC/STATIC_SOURCE/FILESYSTEM/PY_TOOL evidence. R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction. R42 remains the prior counter/route-boundary/proof-label correction. R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers; runtime proof remains absent.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
Current DOC_GLOBAL boundary (2026-05-20 R45): `Docs/Reports/2026-05-20_DOCUMENTATION_R45_ROOT_ARCHITECTURE_R43_R44_RESIDUE_PROOF_ARTIFACTS_AND_COUNTERS_LOCAL.md` is the latest local static root/architecture R43/R44 residue, proof-artifact wording, source-counter, and atlas-boundary correction. R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction. Runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Domain: Habitat & Vehicles / Grid Snapping & Ghost Preview.

The construction socket path now owns a data-oriented snap layer:

- `SocketStateDTO` is 64 bytes, explicit layout, raw fields only.
- Vault lanes `ConstructionSocket*` hold `ConstructionSocketModuleDTO` records, socket states, AUP positions, snap results, tuning, bounds, counters, connection pairs, and the 300-frame black box. Owner-local buffer `70370` stores the active `GhostPreviewDTO`; owner-local buffers `70371` and `70372` store direction CSR ranges and target indices.
- `EvaluateSocketSnappingJob` performs socket delta math in `double3` AUP space, reads only the inverse-direction CSR bucket for each ghost socket, consumes the continuous quality budget as soon as a target row is resolved, rejects invalid direction masks before compatibility math, faults invalid CSR target rows with `NonFinite` instead of aborting the bucket, and writes an aligned `float4x4` snap matrix.
- `AdaptConnectedSocketsJob`, `VerifyModuleBoundsJob`, and `CommitPlacedModuleJob` mutate socket flags and pending topology counters without touching GameObjects.
- Runtime proxy sockets no longer create trigger colliders. Connection visuals route through `Hecton8/Construction/DearLieHologram` and the active `Hecton8/Fabrication/BlueprintWireInstanced` preview shader. `ConstructionPreviewSignal` remains 128 bytes and uses aligned padding offsets 96/100/104 for `DearLieDampen`, `GlobalQualityWeight`, and `DearLieWiggleSpeed`; `HectonBlueprintPreviewBatch` applies those values as a decaying material envelope instead of animating a snapped prefab. Cold fallback proxy materials initialize `_H8SnapDampen` to `0`, so the fake is not permanently active on unsnapped module visuals.

Current migration boundary:

- `PlayerBuilder` uses cached DataVault views and a scheduled deterministic Burst chain instead of `Physics.OverlapSphereNonAlloc` for active structural socket snapping. `EvaluateSocketSnappingJob` is scheduled as an `IJobParallelFor`; `SelectBestSocketSnapJob` depends on that handle, reduces into a reserved sink row in the same `SnapResults` Vault lane, and is finalized only through `DispatcherJobFence.TryFinalizeCompleted`. Target socket hydration is invalidated by module count plus scene hash; pending and cached snap results additionally require a query hash over ghost root, yaw, blueprint hash, and ghost socket layout before a pose can be reused. Blueprint hash uses `ResolveShinobuModuleHash()`, so `ModuleHashId == 0` falls back to `TemplateHashId`; the same fallback is used for `ConstructionPreviewSignal.ModuleHash`, construction validation payloads, acoustic source fallback, and `FloraExclusionSignal.ModuleHash`. Cached Dear Lie pose state is invalidated on query mismatch, no-snap reducer results, failed result application, unsnap, placement reset, and builder reset; `float.MaxValue` cached distance is rejected explicitly. Ghost socket Vault rows preserve source `SocketDefinitions` indices; invalid ghost rows are flagged `NonFinite | CollisionBlocked` and receive zero CSR range rather than being packed away. Invalid authored directions are not quantized to North. When rebuilt, cold `ModuleSocket.IsOccupied` state is transferred into `SocketStateDTO.ConnectionStatus`, then target rows rebuild/validate the direction CSR before reuse. Hot solver matching and cold authoring occupancy use the same `AreCompatibilityHashesCompatible()` predicate; `HashCompatibility()` reserves `0` for wildcard by remapping non-empty folded zero to `1`. After a SHINOBU snap placement, both the target socket and the consumed ghost socket on the newly placed module are marked occupied on the cold authoring components. The per-frame snap decision operates on Vault arrays.
- `ModuleSocket` components remain as cold authoring and occupancy markers until the habitat graph rebuild is fully Vault-fed.
- Vehicle docking triggers are outside this snap-preview route and remain under vehicle docking ownership.

Scalability is continuous through `GlobalQualityWeight`: inspected CSR target-row budget scales from 16 to 256 and search radius scales from the near construction sector to the ultra search radius. The target direction CSR contains open finite sockets only; occupied, blocked, non-finite, and invalid-direction rows are excluded during cold CSR rebuild. The runtime budget is consumed when a CSR target row is read, before radius/compatibility/alignment rejection, so low quality bounds memory bandwidth even when all open sockets are far. Direction CSR removes incompatible buckets before that quality budget is spent. Snap result storage is `64` ghost rows plus one best-result sink row to avoid aliasing.

Verification boundary, 2026-05-20:

- `Hecton8.Core.csproj` now includes SHINOBU socket runtime files, and `Hecton8.Editor.csproj` includes the SHINOBU editor tuner/layout tools.
- `dotnet build Assembly-CSharp.csproj --no-restore --nologo` was reportedly attempted after the CPU gate passed at 29.96 percent. Treat this as `CLI_COMPILE_ATTEMPTED` only until a log path, command, timestamp, environment, and output are linked. The earlier `PlayerBuilder` missing-SHINOBU-type errors are not current clean compile proof.
- Compile remains blocked by the Core.Memory asmdef surface: the referenced `Library/ScriptAssemblies/Hecton8.Core.Memory.dll` is stale and lacks `VaultGenerationHandle<T>`, while source `GlobalDataVault.cs` defines the newer generation-handle API. Regenerating/importing that assembly is a Core.Memory dependency, not a socket-adaptor code path.
