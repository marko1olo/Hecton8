# Status_SHINOBU_209

Agent: SHINOBU_209
Role: OFFLINE_WRECKAGE_GEOMETRY_BAKER
Domain: Echelon 2 World Generation / Offline Wreckage Geometry Baking
Task Count: 20
Status: STATIC IMPLEMENTATION / PROJECT COMPILE BLOCKED OUTSIDE DOMAIN

## Loop 1: Tasks 01-05
- [x] Task 01: REALTIME_DEFORMATION_INQUISITION | Implemented | DOD: static scan covers requested `Combat`, actual `Gameplay/Combat`, and `Environment`; zero forbidden findings in active roots | Alternative rejected: retaining runtime vertex deformation | Estimate: runtime 300-2500 us avoided per destruction event if introduced
- [x] Task 02: RIGIDBODY_DEBRIS_PURGE | Implemented | DOD: scanner catches runtime fragment `Instantiate(` and `AddComponent<Rigidbody>` patterns; zero findings in active roots | Alternative rejected: PhysX fragment spawning | Estimate: runtime 1000-8000 us avoided per debris-heavy breach event
- [x] Task 03: CS1612_GEOMETRY_STATE_ANNIHILATION | Implemented | DOD: bake DTOs expose raw unmanaged fields; source extraction uses `Mesh.AcquireReadOnlyMeshData`; jobs use pointers and `UnsafeUtility.AsRef` | Alternative rejected: property-backed vertex state and managed `List<Vector3>` extraction | Estimate: editor 300-2500 us saved per source mesh plus 5-40 us per 10k vertices versus defensive property copies
- [x] Task 04: ARM64_MAPPING_LAYOUT_ASSERTION | Implemented | DOD: `MeshDamageStateMappingDTO` explicit 32 bytes with offset validator | Alternative rejected: implicit struct layout | Estimate: runtime 0 us added; avoids ARM64 unaligned read trap risk
- [x] Task 05: EMERGENCY_MOCK_DEFORMATION_BENCHMARK | Implemented | DOD: `GenerateMockStructuralDeformationJob` dense-grid shear/twist/blast kernel exists for isolated stress tests | Alternative rejected: waiting on final art assets | Estimate: editor benchmark target 5000-30000 us saved versus managed dense-grid loops

## Loop 2: Tasks 06-10
- [x] Task 06: BURST_STRUCTURAL_SHEAR_KERNEL | Implemented | DOD: `ApplyStructuralShearJob` operates on unmanaged vertex memory with continuous quality weight | Alternative rejected: managed vertex loops | Estimate: editor 1000-6000 us saved per 50k vertices
- [x] Task 07: RADIAL_BLAST_DEFORMATION_MATH | Implemented | DOD: `ApplyRadialBlastJob` computes blast falloff/tear weights; `BuildTornTrianglesJob` duplicates seam vertices and opens degenerate core holes | Alternative rejected: runtime mesh rupture | Estimate: runtime 300-2500 us avoided per blast event
- [x] Task 08: THE_DEAR_LIE_COLLISION_HULLS | Implemented | DOD: `GenerateConvexHullsJob` outputs 8-point support hull under 256-point hard budget | Alternative rejected: complex runtime MeshCollider | Estimate: runtime 200-1200 us avoided during collision-heavy contact
- [x] Task 09: NORMAL_AND_TANGENT_RECALCULATION | Implemented | DOD: `RecalculateDeformedNormalsJob` computes angle-weighted normals/tangents in Burst | Alternative rejected: `Mesh.RecalculateNormals` | Estimate: editor 1000-8000 us saved per large state
- [x] Task 10: ASYNCHRONOUS_ASSET_SERIALIZATION | Implemented | DOD: Forge batch writes Stressed/Ruptured/Collapsed meshes and collider meshes into `Assets/_Project/BakedGeometry/Wreckage` with `SetVertexBufferData`/`SetIndexBufferData` | Alternative rejected: managed mesh array churn | Estimate: runtime 0 us added; editor serialization remains one asset per update tick

## Loop 3: Tasks 11-15
- [x] Task 11: PROCEDURAL_RUST_AND_SCORCH_BAKING | Implemented | DOD: `BakeDamageColorsJob` writes rust/scorch scalar into packed vertex color | Alternative rejected: unique damage textures | Estimate: runtime 0 us added; avoids per-object unique texture fetch/setup
- [x] Task 12: AUP_EPICENTER_LOCALIZATION | Implemented | DOD: double3 blast/module AUP subtraction before local float3 bake math | Alternative rejected: absolute world float blast origin | Estimate: editor avoids non-finite/far-origin corrective reruns
- [x] Task 13: ROLLBACK_NETCODE_EXCLUSION_FENCE | Implemented | DOD: architecture doc states immutable geometry excluded; runtime syncs damage state index only | Alternative rejected: mesh geometry in rollback hash | Estimate: runtime/network 50-400 us avoided per replication tick depending on old payload
- [x] Task 14: ZERO_INIT_OVERHEAD_BYPASS | Implemented | DOD: TempJob scratch buffers use `NativeArrayOptions.UninitializedMemory` and deterministic overwrites | Alternative rejected: MemClear/zero-fill | Estimate: editor 200-1800 us saved per large buffer group
- [x] Task 15: TELEMETRY_DEFORMATION_REPORT_GENERATOR | Implemented | DOD: Forge writes `Docs/Reports/WRECKAGE_BAKE_REPORT.json` after batch with polygon counts, torn vertices, Burst microseconds, warning flags, and `CRITICAL_WARNING` severity | Alternative rejected: chat-only metrics | Estimate: runtime 0 us; editor report cost is cold IO only

## Loop 4: Tasks 16-19
- [x] Task 16: PROCEDURAL_WRECK_FORGE_WINDOW | Implemented | DOD: UI Toolkit `Wreckage Forge` window with folder input, sliders, progress bar, scan, preview, and batch bake | Alternative rejected: ad hoc menu command only | Estimate: developer iteration saved; runtime 0 us
- [x] Task 17: CSV_DESTRUCTION_PROFILES_INGESTOR | Implemented | DOD: byte parser for `wreckage_deformation_profiles.csv` avoids string splitting | Alternative rejected: `string.Split` CSV parsing | Estimate: editor 50-300 us saved per profile load
- [x] Task 18: LIVE_DEFORMATION_PREVIEW_GIZMO | Implemented | DOD: preview deformation uses temporary NativeArrays and editor-only `OfflineWreckagePreviewGizmo` wireframe | Alternative rejected: final asset bake for every tweak | Estimate: developer iteration saved; runtime 0 us
- [x] Task 19: ARCHITECTURAL_METRIC_VALIDATOR | Implemented | DOD: `Runtime_Destruction_Scanner` implemented and `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json` generated with findingCount 0 | Alternative rejected: manual grep evidence only | Estimate: prevents 300-8000 us/event regressions by static enforcement

## Loop 5: Strict Self-Audit / Task 20
- [x] Task 20: SELF_AUDIT_AND_ARCHITECTURE_VERIFICATION | Implemented | DOD: final log appended with `<SELF_AUDIT>` block; compile status explicitly gated by CPU load | Alternative rejected: fake compile pass declaration | Estimate: runtime 0 us added

## Verification
- Compile: Not run. Latest `Get-CimInstance Win32_Processor` reported 100% load; per project rule, no dotnet build was launched while CPU was above 50%.
- Dotnet/csc contention: `Get-Process dotnet,csc` returned no active process at check time.
- Pass 3 build gate: `Get-Counter '\Processor(_Total)\% Processor Time'` returned 100. `tasklist` process check timed out under load. No build or rebuild was launched.
- Pass 4 build gate: `Get-Counter '\Processor(_Total)\% Processor Time'` returned 97.856. `Get-Process dotnet,csc` returned no active process. No build or rebuild was launched because CPU was still above 50%.
- Pass 4 final build gate: `Get-Counter '\Processor(_Total)\% Processor Time'` returned 100. `Get-Process dotnet,csc` returned no active process. No build or rebuild was launched.
- Pass 6 build gate: `Get-Counter '\Processor(_Total)\% Processor Time'` returned 100. `Get-Process dotnet,csc` returned no active compiler process. No build or rebuild was launched.
- Pass 8 build gate: `Get-Counter '\Processor(_Total)\% Processor Time'` returned 100. `Get-Process dotnet,csc` returned no active compiler process. No build or rebuild was launched.
- Pass 9 build gate: `Get-Counter '\Processor(_Total)\% Processor Time'` returned 76.147. `Get-Process dotnet,csc` returned no active compiler process. No build or rebuild was launched because CPU remained above 50%.
- Pass 10 build gate: `Get-Counter '\Processor(_Total)\% Processor Time'` returned 100. `Get-Process dotnet,csc` returned no active compiler process. No build or rebuild was launched.
- Pass 11 build attempt: one single-core `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` was launched only after CPU measured 45.095 percent and `Get-Process dotnet,csc` returned no active compiler process. It stopped on 72 `Hecton8.Core.csproj` errors outside `Assets/_Project/Scripts/World/OfflineWreckageBaker`; no SHINOBU_209-owned compile error appeared in the emitted error list.
- Static scan: Run via PowerShell mirror of scanner patterns; active roots findingCount=0.
- Reports: `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json` regenerated for SHINOBU_209 with SHINOBU_210 preservation metadata; sidecar `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_209.json` exists. `Docs/Reports/WRECKAGE_BAKE_REPORT.json` is generated by the Forge after an actual selected-folder batch bake; no asset batch was run in this session.

## Ultra-Think Polish Pass
- [x] Preview fence fixed | DOD: `previewHandle.Complete()` executes before preview mesh construction | Rejected: reading NativeArray counts before job completion | Estimate: runtime 0 us, editor race removed
- [x] AUP UI precision fixed | DOD: Forge uses `DoubleField` AUP controls and double subtraction before local float cast | Rejected: `Vector3Field` float AUP entry | Estimate: avoids full rebake loss from far-origin precision failure
- [x] Burst flags corrected | DOD: owned mathematical jobs use `FloatMode.Fast` and `FloatPrecision.Standard` | Rejected: deterministic mode for non-authoritative offline asset baking | Estimate: editor 5-40 percent math-kernel gain target
- [x] Cold byte-array allocations removed | DOD: CSV and mapping byte buffers use `stackalloc Span<byte>` | Rejected: `File.ReadAllBytes`/`File.WriteAllBytes` | Estimate: 10-200 us editor cold IO allocation overhead avoided per file
- [x] Preview retained NativeArrays removed | DOD: preview store owns a temporary Mesh, not persistent NativeArrays | Rejected: static persistent preview buffers | Estimate: runtime 0 us, editor leak surface reduced
- [x] Ultra-Think forensic log appended | DOD: `Docs/AgentLogs/LOG_SHINOBU_209.md` now includes explicit 20-task reconciliation, struct layout math, scalability curve, H-Phi runtime/editor split, dependency graph, compile guard, and Dear Lie Big-O audit | Rejected: terse chat-only audit | Estimate: runtime 0 us

## Ultra-Think Polish Pass 2
- [x] Persistent profile NativeArray removed | DOD: Forge CSV profiles live in fixed 16-slot `WreckageProfileCache`, not `Allocator.Persistent` native state | Rejected: retaining `_profiles` as editor-native state for 16 rows | Estimate: runtime 0 us, editor retained native bytes reduced
- [x] 64-byte bake counters added | DOD: `OfflineWreckageBakeCounters64` replaces small `NativeArray<int>` count/hull-count buffers and is layout-validated | Rejected: adjacent tiny int counter arrays | Estimate: runtime 0 us, editor false-sharing proof improved

## Ultra-Think Polish Pass 3
- [x] Atomic binary/report writes added | DOD: damage-state `.bytes`, Forge JSON report, scanner canonical report, and scanner sidecar write through `.tmp`; existing targets now publish via `File.Replace` | Rejected: direct file overwrite that can leave torn metadata after Editor interruption | Estimate: runtime 0 us, editor correctness hardening
- [x] Black-box dump writer de-objectified | DOD: `OfflineWreckageBlackBox` no longer uses `BinaryWriter`; it writes a fixed 32-byte header and raw 64-byte telemetry rows through stack spans and `UnsafeUtility.CopyStructureToPtr` | Rejected: per-field managed writer facade | Estimate: runtime 0 us, editor dump path allocation/noise reduced

## Ultra-Think Polish Pass 4
- [x] CI mock benchmark entrypoint added | DOD: `OfflineWreckageMockBenchmark` runs dense-grid mock vertices, generated surface indices, shear, radial blast, tear, normal, color, and hull jobs, then writes `Docs/Reports/WRECKAGE_MOCK_BENCHMARK_SHINOBU_209.json` atomically | Rejected: job-only mock kernel with no automation surface | Estimate: runtime 0 us, CI/editor benchmark coverage added
- [x] Job alias annotations tightened | DOD: output-only NativeArray fields gained `[WriteOnly]` in extraction, mock index generation, copy destination, mock vertex generation, tear weights, torn mesh outputs, counter row, and hull points | Rejected: relying on `[NoAlias]` alone | Estimate: runtime 0 us, Burst alias proof improved
- [x] Pass 4 static scans rerun | DOD: owned baker scan excluding scanner pattern constants found no forbidden mesh APIs, random APIs, final-path write helpers, `Pack=`, DTO auto-properties, `FloatMode.Deterministic`, `foreach`, or LINQ `.ToList()`; `git diff --check` reported no whitespace errors | Rejected: chat-only evidence | Estimate: runtime 0 us
- [x] Stable Unity metas added | DOD: every SHINOBU_209 `.cs` and `.asmdef` file in `OfflineWreckageBaker` has an explicit `.meta`; duplicate GUID scan returned no duplicates | Rejected: letting Unity mint GUIDs during first import | Estimate: runtime 0 us, import determinism improved

## Ultra-Think Polish Pass 5
- [x] Invariant JSON numeric reports added | DOD: Forge and mock benchmark floating microsecond fields now use `CultureInfo.InvariantCulture`; scan found no remaining `ToString("0.000")` without invariant culture in owned baker C# | Rejected: locale-dependent comma decimals that corrupt machine-readable JSON evidence on non-US Windows locales | Estimate: runtime 0 us, report correctness hardening
- [x] Publish gap removed from atomic artifact writes | DOD: damage-state `.bytes`, Forge report, scanner reports, mock benchmark report, and black-box dump now use `File.Replace(temp, final, null)` when the final file already exists and `File.Move` only for first creation | Rejected: `File.Delete(final) + File.Move(tmp, final)` because it creates a reader-visible missing-file gap | Estimate: runtime 0 us, editor artifact integrity hardening
- [x] Dense mock benchmark now covers six cube faces | DOD: `GenerateMockGridSurfaceIndicesJob` now emits XY/XZ/YZ min/max surface triangles; benchmark index count for 48x48x6 is 32148 instead of one-surface 13254 | Rejected: single XY-surface mock that under-exercised normal, tear, and hull paths | Estimate: runtime 0 us, CI/editor coverage hardening
- [x] SHINOBU_209 architecture docs refreshed | DOD: route card and binary ledger now describe `File.Replace` publication and six-face mock benchmark indices | Rejected: stale doc claiming final move and XY-only surface | Estimate: runtime 0 us, integration proof accuracy
- [x] Pass 5 build gate respected | DOD: latest `Get-Counter '\Processor(_Total)\% Processor Time'` returned 59.094 and `Get-Process dotnet,csc` returned no active compiler process; no dotnet build/rebuild was launched because CPU remained above 50% | Rejected: violating CPU >50% build guard | Estimate: runtime 0 us

## Ultra-Think Polish Pass 6
- [x] Preview lifecycle disposal added | DOD: editor preview temp mesh now has `HideFlags.HideAndDontSave` and is disposed before assembly reload/editor quit; black-box NativeArray ring is also disposed at the same editor lifecycle boundary | Rejected: relying on Unity domain teardown to clean retained temp editor objects | Estimate: runtime 0 us, editor leak surface reduced
- [x] Stale evidence wording scrubbed | DOD: SHINOBU status/rationale/log wording now matches existing `File.Replace` publication instead of `.tmp` final-move language | Rejected: stale proof text that overstates a weaker publication path | Estimate: runtime 0 us

## Ultra-Think Polish Pass 7
- [x] NativeDisableParallelForRestriction invariants documented | DOD: every owned use in `OfflineWreckageBakeJobs.cs` now states the per-index write or disjoint-buffer invariant at the suppression site | Rejected: relying on reader inference for Burst safety suppression | Estimate: runtime 0 us, review proof hardening

## Ultra-Think Polish Pass 8
- [x] Fixed-temp artifact reuse removed | DOD: mapping bytes, Forge reports, scanner reports, mock benchmark report, and black-box dump now route through `OfflineWreckageAtomicFile` unique same-volume `.tmp.<processId>.<ordinal>` paths with `FileMode.CreateNew`; final targets publish through `File.Replace` or first-create `File.Move` | Rejected: fixed `path + ".tmp"` reuse and deleting shared temp files before writes | Estimate: runtime 0 us, editor concurrency/corruption risk reduced

## Ultra-Think Polish Pass 9
- [x] Stable baked asset identity enforced | DOD: generated mesh, collider, and damage-map paths now use deterministic `GEN_<safeSource>_<sourceHash>` names; existing mesh assets refresh with `EditorUtility.CopySerialized` to preserve `.meta` GUIDs | Rejected: `AssetDatabase.GenerateUniqueAssetPath` per rebake because it creates orphaned numbered assets and breaks reference stability | Estimate: runtime 0 us, editor/import reference churn reduced

## Ultra-Think Polish Pass 10
- [x] Binary padding determinism fixed | DOD: 32-byte mapping payload stack span and 32-byte black-box dump header are explicitly cleared before field writes | Rejected: relying on uninitialized stack padding bytes | Estimate: runtime 0 us, serialized proof determinism restored

## Ultra-Think Polish Pass 11
- [x] Multi-submesh source extraction fixed | DOD: all triangle submeshes are collapsed into explicit 16-byte source index ranges with `baseVertex` applied | Rejected: `subMesh(0)`-only extraction because it silently drops material sections | Estimate: runtime 0 us, editor correctness
- [x] Index copy range lookup removed | DOD: 384-index triangle-aligned tiles schedule disjoint output windows, changing copy work from O(indexCount * submeshCount) lookup to O(indexCount) copy | Rejected: per-index range scan inside Burst | Estimate: runtime 0 us, editor copy cost reduced on multi-material meshes
- [x] Pass 11 static scans rerun | DOD: old-symbol scan clean, direct sibling reference scan clean, forbidden API scan only finds scanner constants, `git diff --check` reports only CRLF warning in an existing doc | Rejected: unverified source edit | Estimate: runtime 0 us
- [x] Pass 11 compile gate attempted | DOD: one single-core dotnet build launched only after CPU 45.095 percent and dotnet/csc none; build stopped on 72 existing Core errors outside SHINOBU_209 domain; no owned-domain error appeared in emitted output | Rejected: repeated builds against unrelated compile wall | Estimate: runtime 0 us

## Ultra-Think Polish Pass 12
- [x] 16-bit baseVertex overflow path clamped | DOD: `CopyIndex16RangesJob` now uses the same long-add and int clamp discipline as the 32-bit path | Rejected: unchecked `ushort + baseVertex` addition because pathological importer metadata can wrap before the tear job degenerates invalid triangles | Estimate: runtime 0 us, editor corrupt-import hardening
- [x] Submesh descriptor bounds sanitized | DOD: `BuildTriangleSubMeshRanges` clamps `indexStart` to the source index buffer length and truncates available count to whole triangles before tile emission | Rejected: trusting descriptor count blindly | Estimate: runtime 0 us, editor out-of-range read prevention
- [x] Pass 12 static scans rerun | DOD: stale unchecked-add scan clean except the intentional new long-add, forbidden API scan only finds scanner constants, `git diff --check` reports only CRLF warning in an existing doc | Rejected: undocumented source hardening | Estimate: runtime 0 us

## Ultra-Think Polish Pass 13
- [x] Black-box native ring tracking bridged | DOD: editor black-box `Allocator.Persistent` ring registers/unregisters through `NativeMemoryTrackingBridge` from `Hecton8.Core.Contracts` | Rejected: untracked persistent editor NativeArray and direct `Hecton8.Core` dependency | Estimate: runtime 0 us, editor leak-audit proof improved
- [x] Compile-wall guard preserved | DOD: only the Editor asmdef gained `Hecton8.Core.Contracts`; runtime asmdef remains isolated and no sibling runtime domain reference was introduced | Rejected: root Core reference from offline baker | Estimate: runtime 0 us

## Ultra-Think Polish Pass 14
- [x] Thin-hull collision lie preserved measured extents | DOD: support hull generation now expands only degenerate axes to 0.01 m half-extents instead of replacing any flat plate with a generic unit cube | Rejected: unit-cube fallback for bulkheads/hull plates and runtime detailed MeshCollider truth | Estimate: runtime 0 us, editor collision proxy correctness hardened
- [x] Hull expansion warning surfaced | DOD: `WarningHullBoundsExpanded` bit flows through the 64-byte counter row into report/black-box warning flags | Rejected: silently modifying collision proxy thickness with no forensic evidence | Estimate: runtime 0 us

## Ultra-Think Polish Pass 15
- [x] Scanner report recursion bounded | DOD: `Runtime_Destruction_Scanner` no longer embeds the entire prior canonical JSON inside the next canonical report; it writes bounded previous-report byte/hash/agent fields and snapshots the prior JSON to `PHYSICS_OPTIMIZATION_REPORT_PREVIOUS_SHINOBU_209.json` | Rejected: recursive `previousReport` blob growth in a shared canonical report | Estimate: runtime 0 us, editor report size bounded
- [x] Prior-report provenance retained | DOD: previous report preservation remains explicit through hash, byte count, agent string, and sidecar copy | Rejected: deleting other-agent canonical evidence before overwrite | Estimate: runtime 0 us

## Ultra-Think Polish Pass 16
- [x] Scanner byte provenance corrected | DOD: `previousReportBytes` now measures UTF-8 encoded bytes matching `WriteTextUtf8`, and `previousReportHash` hashes that same UTF-8 byte stream; source scan found no remaining scanner-source `previousReport.Length`/`HashText` path | Rejected: `string.Length` UTF-16 code-unit count mislabeled as bytes | Estimate: runtime 0 us, editor report correctness hardened

## Ultra-Think Polish Pass 17
- [x] Scanner hash normalization removed | DOD: `previousReportHash` now uses local raw FNV byte updates for every emitted UTF-8 byte instead of `OfflineWreckageBakeMath.HashBytes` name-normalization semantics | Rejected: lowercasing/skipping whitespace inside a provenance hash | Estimate: runtime 0 us, editor artifact hash correctness hardened

## Ultra-Think Polish Pass 18
- [x] Scanner JSON escaping hardened | DOD: report string fields now escape JSON control characters and `ExtractJsonStringValue` checks backslash parity before accepting a closing quote | Rejected: quote/backslash-only escaping and single-backslash quote detection | Estimate: runtime 0 us, editor report correctness hardened

## Ultra-Think Polish Pass 19
- [x] Scanner previous-agent extraction fails closed on non-string values | DOD: `ExtractJsonStringValue` now skips JSON whitespace after `:` and requires an immediate string quote before parsing | Rejected: scanning forward to the next quoted key/value after a non-string `agent` field | Estimate: runtime 0 us, editor provenance correctness hardened

## Ultra-Think Polish Pass 20
- [x] Atomic artifact publish race hardened | DOD: `OfflineWreckageAtomicFile.Publish` retries once against post-failure file state after `FileNotFoundException`/`IOException` if the owned temp still exists | Rejected: single `File.Exists` snapshot before `File.Replace`/`File.Move` under parallel Editor writers | Estimate: runtime 0 us, editor artifact publication resilience hardened

## Ultra-Think Polish Pass 21
- [x] Normal angle NaN guard hardened | DOD: `RecalculateDeformedNormalsJob.Angle` now rejects non-finite or near-zero edge lengths before `math.rsqrt` and returns 0 on non-finite dot products | Rejected: relying only on upstream vertex sanitization | Estimate: runtime 0 us, editor normal accumulation safety hardened

## Ultra-Think Polish Pass 22
- [x] Deformation scalar NaN guards hardened | DOD: mock deformation, structural shear, radial blast, torn triangle duplication, and damage-color baking now sanitize finite quality/radius/torsion/damage/intensity inputs before sqrt/rsqrt/rcp/smoothstep math | Rejected: trusting Forge UI/CSV/imported mesh inputs to stay finite | Estimate: runtime 0 us, editor corrupt-profile hardening
- [x] Tear smoothstep divide-by-zero path fenced | DOD: `BuildTornTrianglesJob` skips visual tear duplication when threshold is effectively 1.0 instead of calling `math.smoothstep(threshold, 1f, tear)` with equal edges | Rejected: relying on `math.rcp(max(...))` in the tear-weight job while leaving the later visual split path unfenced | Estimate: runtime 0 us, editor NaN prevention

## Ultra-Think Polish Pass 23
- [x] Counter row allocator zero-fill removed | DOD: Forge preview, Forge bake, and mock benchmark now allocate the single `OfflineWreckageBakeCounters64` TempJob row with `UninitializedMemory`; `BuildTornTrianglesJob` fully overwrites `Counters[0]` before hull/report reads | Rejected: `NativeArrayOptions.ClearMemory` for a deterministic 64-byte job output row | Estimate: runtime 0 us, editor 64B memset avoided per preview/bake/mock run
