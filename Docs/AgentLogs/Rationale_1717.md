# Rationale 1717

Problem: `ProceduralWreckGenerator` still exposed runtime-visible mesh construction routes.
Solution: fenced merged mesh fallback and mesh data jobs with `UNITY_EDITOR`, removed proxy mesh generation, and kept runtime proxy as serialized `wreckCollisionProxyMesh`.
Rejected Alternatives: runtime `bool` guard only; too weak because code remains in player build and can drift back into hot execution.
Scalability potential: low devices stream baked geometry only; high/ultra devices get denser offline variants through `GlobalQualityWeight` without runtime truth changes.
Hardware Impact: prevents main-thread mesh allocation/build stalls on i3/MX350 class hardware.

Problem: CSG hull holes were indistinguishable from invalid degenerate triangles.
Solution: added `FractureHoleTriangleCount` and warning bit while preserving 64-byte counter layout.
Rejected Alternatives: overloading `DegenerateTriangleCount`; that corrupts topology validation semantics.
Scalability potential: all tiers can report intentional damage density; higher tiers can bake more holes safely offline.
Hardware Impact: no runtime cost; faster editor diagnosis.

Problem: baked soot/grime masks could be lost by material routing.
Solution: shader reads soot from vertex alpha; forge only assigns `Hecton8/World/WreckIndirectLit` materials or one shared default fallback.
Rejected Alternatives: arbitrary source materials; they can ignore vertex color alpha/blue channels and break wreck readability.
Scalability potential: one material can scale visual complexity through baked mesh data and shader weights.
Hardware Impact: stable SRP batching; avoids material-slot growth.

Problem: wide folder bakes could ingest generated wreck outputs.
Solution: source filter rejects `GEN_`, `_COLLIDER`, output mesh folder, and output prefab folder.
Rejected Alternatives: relying on artist folder discipline.
Scalability potential: safe batch bakes across large content folders.
Hardware Impact: prevents runaway editor bake time and asset churn.

Problem: prefab publish could count a mesh as processed even if salvage metadata/material/collider contracts failed.
Solution: made prefab serialization return a hard boolean gate, validate `EquipmentMetadata` anchor layout and anchor set before save, reject any `MeshCollider`, require at least one solid primitive collider, and cap every renderer to 1-2 `Hecton8/World/WreckIndirectLit` material slots.
Rejected Alternatives: save prefab and log a warning; that ships broken salvage sockets or renderer state into content.
Scalability potential: low/mid/high/ultra all receive the same primitive physics and metadata contract; visual density scales only in baked mesh variants.
Hardware Impact: prevents runtime salvage lookup failures and physics mesh collider stalls on i3/MX350 class hardware.

Problem: Agent 1717 entrypoint was a passive window route instead of a direct bake command for selected source assets.
Solution: added `HECTON-8/Wreckage Forge/Bake Selected Assets`, queued selected pristine meshes/prefabs/folders through the existing Forge, and exposed it from `WreckageFractureEngine1717`.
Rejected Alternatives: a parallel `WreckageFractureEngine` algorithm class; duplicating Voronoi/CSG logic would split validation ownership.
Scalability potential: all quality lanes keep one offline bake route; artists can rebake selected low/mid/high/ultra variants without touching runtime.
Hardware Impact: no player-frame cost; reduces editor setup churn and prevents accidental ad hoc runtime fracture tooling.

Problem: Forge selection/folder bake commands could be invoked again while the editor batch was still active.
Solution: added a cold `RejectActiveBake` gate and made `CreateGUI` idempotent before any command-driven bake starts.
Rejected Alternatives: clearing the queue on every command, because that can erase pending pristine assets mid-bake.
Scalability potential: low/mid/high/ultra bake lanes keep deterministic batch ownership and one update callback.
Hardware Impact: editor-only; prevents duplicated bake callbacks and wasted asset import churn.

Problem: deformed wreck normals were recalculated after topology edits but did not carry a visible fracture-peel bias.
Solution: inserted `BendFractureNormalsJob` after normal recalculation, using the existing pseudo-Voronoi edge field to bend normals offline before vertex color mask baking.
Rejected Alternatives: runtime normal deformation or a duplicate Voronoi helper; both would violate the offline-only owner route.
Scalability potential: weak devices stream cheap baked normals; high/ultra variants can bake stronger seam peel through continuous `GlobalQualityWeight`.
Hardware Impact: 0 us player cost; bake cost is Burst/data-local and amortized in editor.

Problem: `WreckMaterialRegistry` created a BRG handle buffer while holding the DataVault metadata write lock.
Solution: hoisted `CreateBatchHandleBuffer` before `TryAcquireBatchMetadata`, released it on lock acquisition failure, and kept release in `finally`.
Rejected Alternatives: accepting the cold allocation inside the lock; graphics buffer creation can block unrelated vault compaction fences.
Scalability potential: all hardware tiers keep the same lock discipline; visual density remains controlled by baked variants and BRG capacity, not by longer vault locks.
Hardware Impact: reduces cold-start stall risk on i3/MX350 class hardware without changing runtime draw ownership.

Problem: the 1717 facade briefly risked a direct reference from `Hecton8.Project.Editor` into the separate `Hecton8.World.OfflineWreckageBaker.Editor` asmdef.
Solution: kept selected-source validation inside `WreckageForgeWindow`, exposed it as a menu item, and made `WreckageFractureEngine1717` call menu routes only.
Rejected Alternatives: adding a new asmdef reference from project editor tools to the offline wreckage baker; that widens assembly topology for a UI shortcut.
Scalability potential: source validation remains one owner route for low/mid/high/ultra bake variants; facade stays a thin operator panel.
Hardware Impact: editor-only; avoids failed delayed bake setup and prevents unnecessary window/update creation for invalid selections.
