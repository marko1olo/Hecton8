# Rationale_1604

Status: IMPLEMENTED / BLOCKED BY EXTERNAL COMPILE STATE

## Decision 00 - Scope Boundary

Problem: Prompt demands flora generation, LOD assets, vertex color sway, and prefabs; runtime scatter, runtime physics, and renderer ownership are separate domains.
Solution: Keep implementation under `Assets/_Project/Editor/Generators/Flora/` and output static mesh/prefab assets under project asset folders. Runtime consumes baked assets only.
Rejected Alternatives: Runtime mesh generation, runtime L-system expansion, SkinnedMeshRenderer plants, and per-blade physics. They violate zero-runtime-generation and scatter budgets.
Scalability potential: Low = simple low-tri LOD2/cards; Middle = LOD1 with vertex color sway; High = dense LOD0 and richer masks; Ultra = larger preset batches and higher near-field residency.
Hardware Impact: Low-end i3/MX350 gains from no runtime geometry generation, no CPU skinning, no per-frame allocation, and coarse colliders only.

## Decision 01 - Proof Artifacts

Problem: Prompt requests JSON reports, but current user explicitly rejects useless JSON/binary dumps; project rules still require status/rationale/final logs.
Solution: Use `Status_1604.md`, `Rationale_1604.md`, `LOG_1604.md`, and generated prefab/mesh assets as primary evidence. JSON report remains optional/self-control only if a validator requires it.
Rejected Alternatives: Spending engineering time on report-only payloads without production assets.
Scalability potential: Logs stay concise; asset proof scales by generated prefab count and deterministic mesh hashes.
Hardware Impact: No runtime impact; avoids repository clutter and wasted CPU.

## Decision 02 - Generator Architecture

Problem: Existing flora authoring code already emits many BioForge assets but has managed `List<T>` mesh cores and does not provide the requested L-system topology lane.
Solution: Add a new editor-only `Generators/Flora` lane with deterministic DTOs, native fixed-capacity buffers, MeshData serialization, LOD sampling, vertex color masks, and prefab assembly. Existing assets remain untouched.
Rejected Alternatives: Refactor the legacy seaweed/coral builders in-place during a parallel batch; that would risk breaking existing generated flora families and broaden blast radius.
Scalability potential: Low = 3-sided/short depth LOD2 tubes/cards; Middle = moderate ring/branch counts; High = dense branching and glow masks; Ultra = larger seed batches and higher LOD0 budgets without changing runtime truth.
Hardware Impact: Low-end i3/MX350 avoids runtime generation and receives smaller LOD meshes; high-end gets richer near-field mesh residency purchased with saved CPU.

## Decision 03 - First 20 Minutes Route

Problem: Flora topology is content breadth unless tied to route proof.
Solution: Treat generated flora as world-load/resource readability support: kelp silhouettes, coral/tube-worm harvest proxies, and shader masks for pressure/bioluminescent route cues.
Rejected Alternatives: Broad ecosystem simulation or runtime scatter ownership changes; those do not directly prove the Copper Wire route.
Scalability potential: Assets can be scattered by existing placement rules and scaled by continuous quality via LOD residency/material detail, not different gameplay facts.
Hardware Impact: Static prefab/LOD output keeps compact route load predictable and leaves visual overkill to renderer/scatter quality settings.

## Decision 04 - Fixed Native Capacity Over Dynamic NativeList Growth

Problem: The prompt names `NativeList<float3>`, but uncontrolled growth in an editor geometry batch can hide capacity mistakes and make failure modes softer than the project allows.
Solution: Use fixed-capacity `NativeArray<T>` buffers sized from `FloraGenomeDTO`, with explicit overflow counters and fail-closed exits. This makes complexity limits visible and deterministic.
Rejected Alternatives: Managed `List<T>`, recursive strings, `NativeList<T>` growth during core generation, or best-effort truncation.
Scalability potential: Low = small capacities and low radial segments; Middle = moderate branch counts; High = larger node buffers; Ultra = high seed batches with the same fixed ownership model.
Hardware Impact: Low-end i3/MX350 avoids allocator churn and editor stalls; high-end hardware can buy denser LOD0 meshes without changing memory ownership.

## Decision 05 - LOD Decimation Route

Problem: Full QEM edge-collapse on generated cylindrical branch meshes is expensive to implement safely under current compile contention and can collapse external branch extremities if weights are wrong.
Solution: Preserve skeleton extremities and reduce radial topology per LOD, then run a hard bounding volume/center-shift silhouette scanner before saving assets. This gives predictable scatter LODs without corrupting outer shape.
Rejected Alternatives: Fake QEM label, dropping branch edges by stride, or accepting LOD2 volume shrink beyond 10%.
Scalability potential: Low = 3 radial segments at LOD2; Middle = 4-5 segments at LOD1; High = denser LOD0; Ultra = QEM can be added later behind the same silhouette gate if profiler proof justifies it.
Hardware Impact: Low-end i3/MX350 receives fewer vertices per ring and no runtime simplification; high-end keeps richer near-field shape.

## Decision 06 - Vertex Animation Route

Problem: The user requested sway/VAT behavior, but the available stable scatter contract in this lane is vertex-color-driven analytic sway. The `ProceduralFamily_Flora` VAT descriptor exists in a runtime asmdef not referenced by `Hecton8.Project.Editor`.
Solution: Deliver mandatory shader-ready vertex colors now: R root sway, G deterministic phase, B glow mask. Do not fake a bound VAT asset without the shader/family binding route. VAT texture binding remains a separate integration step once the asmdef route and shader property names are confirmed.
Rejected Alternatives: SkinnedMeshRenderer/Animator, CPU sway, unbound placeholder VAT textures, or changing asmdef dependencies during an unrelated compile break.
Scalability potential: Low = analytic sway from vertex colors; Middle = richer material response from phase/glow; High = optional authored VAT payload; Ultra = dense VAT or compute animation once the renderer contract exists.
Hardware Impact: Low-end i3/MX350 gets zero CPU animation and compact vertex color payload; high-end can consume optional VAT later without altering baked mesh identity.

## Decision 07 - Compilation Gate

Problem: A project build is explicitly forbidden after small edits, and Unity reports an unrelated compile error in `PrologueOrbitSceneBootstrap.cs`.
Solution: Do not run `dotnet build` for small verifier/API cleanup. Use Unity console filtering for `FloraTopology1604`, static grep, brace count, SHA-256, and task ledger proof. Mark execution tests blocked by dependency/policy rather than claiming them.
Rejected Alternatives: Burning host CPU with a forbidden build, fixing another agent's prologue assembly issue without ownership, or fabricating generated prefab proof.
Scalability potential: Clean compile gate lets menu execution and seed-pack baking resume without changing 1604 code.
Hardware Impact: Avoids unnecessary compile churn on the shared machine and prevents cascading stalls for the parallel agent cluster.

## Decision 08 - Report Format

Problem: Batch task asks for a JSON report, but the user explicitly rejected unread JSON/binary dumps and ordered proof by generated assets and concise logs.
Solution: Treat `Status_1604.md`, `Rationale_1604.md`, final `LOG_1604.md`, SHA-256, and eventual mesh/prefab assets as the proof route. No JSON report is emitted in this pass.
Rejected Alternatives: Writing `Docs/Reports/FLORA_TOPOLOGY_GENERATION_1604.json` only to satisfy bureaucracy after the user explicitly canceled that artifact.
Scalability potential: The same metric fields exist in code and can be serialized later if an actual validator consumes them.
Hardware Impact: No runtime impact; avoids disk clutter and pointless editor IO.

## Decision 09 - Apex Integrator Verification

Problem: Verbal compliance claims do not prove hot lookup, phase, or write-lock safety.
Solution: Add `FloraApexIntegratorVerifier1604` as an editor menu scanner. It scans the 1604 generator, flora ambient sway, and procedural coral lanes for hot `GlobalRegistry.Get<T>()` / `GetComponent()` calls, presentation uploads outside `VisualSyncTick`/`LateFrameTick`, nested DataVault write locks, missing `try/finally`, and active compiler contention.
Rejected Alternatives: Chat-only assertions, broad project rewrite outside domain, or running `dotnet build` while `dotnet` is active.
Scalability potential: Low = static scanner keeps low-end authoring machines from wasting builds; Middle = scan runs before menu baking; High = more flora/coral lanes can be added to `ScanTargets`; Ultra = same rule set can become an EditMode gate after compile state is clean.
Hardware Impact: Low-end i3/MX350 avoids build contention and catches architecture drift before Unity import cycles.

## Decision 10 - MeshData API Correction

Problem: Passing a `NativeArray<VertexAttributeDescriptor>` to `MeshData.SetVertexBufferParams` is a compile-risk because project examples use Unity's `params VertexAttributeDescriptor` API.
Solution: Replace the temporary native layout array with explicit `VertexAttributeDescriptor` parameters matching existing mesh baker patterns, and add an exception path that disposes un-applied writable MeshData.
Rejected Alternatives: Waiting for compiler failure, using managed `mesh.vertices`, or keeping an API ambiguity in the core serialization path.
Scalability potential: Low = safer editor import; Middle = fewer Unity API edge failures; High = layout remains stream-separated for bulk MeshData writes; Ultra = compatible with future packed vertex layout optimization.
Hardware Impact: No runtime cost; authoring machines avoid a failed import loop.

## Decision 11 - Editor UI Surface

Problem: `OnGUI` is legacy IMGUI and conflicts with current project editor-surface expectations.
Solution: Use UIElements `CreateGUI` with typed fields and buttons for Generate, Determinism Test, and Apex Verify.
Rejected Alternatives: Keeping IMGUI or moving the generator to command-only menus.
Scalability potential: Low = clean manual generation; Middle = less Editor repaint noise; High = easy addition of preset batch controls; Ultra = can host asset preview once compile state allows generation.
Hardware Impact: No runtime cost; reduces editor UI churn during authoring.

## Decision 12 - Literal-Safe Static Verification

Problem: A text scanner that reads raw source lines can mistake string constants or comments for real hot-path dependency calls.
Solution: Strip string literals, char literals, verbatim strings, `//` comments, and multi-line block comments before method, lookup, phase, and lock analysis.
Rejected Alternatives: Treating `rg` hits on verifier token strings as architectural proof, or relying on chat assertions.
Scalability potential: Low = fewer false positives during small audits; Middle = cleaner manual verification; High = scanner can become an EditMode gate; Ultra = same sanitizer can front a Roslyn analyzer replacement if needed.
Hardware Impact: Editor-only allocation is acceptable; it avoids wasted Unity/import/build cycles on false findings.

## Decision 13 - Axial LOD Instead Of Dead EdgeStride

Problem: `PathSegments` and `EdgeStride` were not materially affecting generated topology. That made LOD reduction weaker than the stated asset contract.
Solution: Remove dead `EdgeStride`, use `PathSegments` to generate axial tube subdivisions, and reduce both axial slices and radial sides across LOD0/1/2 while preserving all skeleton endpoints.
Rejected Alternatives: Deleting every Nth skeleton edge, because it creates visible gaps and branch endpoint loss; leaving dead parameters in production C#.
Scalability potential: Low = LOD2 keeps one axial slice and three radial sides; Middle = LOD1 keeps two axial slices; High = LOD0 keeps richer axial gradients; Ultra = future QEM can run after this deterministic pre-decimation if profiler proof justifies it.
Hardware Impact: Low-end i3/MX350 gets lower far-field vertex count without silhouette collapse; high-end gets smoother near-field sway gradients.

## Decision 14 - Controlled 100K Fuzzer

Problem: After axial subdivisions were wired, the previous fuzzer settings could push far beyond the required 100k vertices, wasting editor CPU and weakening the 100ms target.
Solution: Set the fuzzer to depth 6, 6000 nodes, 5999 edges, radial 6, axial path 4. The generated LOD0 target remains above 100k vertices but avoids pointless 400k+ stress.
Rejected Alternatives: Leaving an uncontrolled fuzzer that proves machine abuse instead of generator robustness.
Scalability potential: Low = controlled editor validation on weak machines; Middle = repeatable fuzzer size; High = can raise capacity manually when profiler proof needs it; Ultra = multi-seed fuzzer batch later, still bounded.
Hardware Impact: Low-end i3/MX350 avoids excessive editor stress while still validating the >100k path.

## Decision 15 - Unity Meta Hygiene

Problem: The new C# asset meta existed but lacked the `MonoImporter` block, which risks unstable Unity import behavior and GUID churn.
Solution: Preserve the generated GUID and add the standard `MonoImporter` section with empty references, execution order 0, and no bundle metadata.
Rejected Alternatives: Letting Unity silently regenerate or mutate importer metadata on first clean import.
Scalability potential: Low = stable source import; Middle = safer parallel agent worktree; High = predictable references if tools later link to this editor script; Ultra = no GUID churn in CI.
Hardware Impact: No runtime impact; reduces editor reimport churn.

## Decision 16 - Attribute-Aware Prompt Revalidation

Problem: The live batch prompt uses `<AGENT_PROMPT id="1604" role="..." chat_name="1604">`; a bare `<AGENT_PROMPT id="1604">` regex falsely reports missing prompt and can push an agent toward the wrong neighboring task.
Solution: Use an attribute-aware CLI regex keyed only on the `id="1604"` attribute and count `Task NN:` markers from the extracted block. Latest extraction returned 20 tasks and SHA-256 `6876EAA0AC2F41463148219012580F72002644C838DFEB1FD9EF4F6033FDBF1D`.
Rejected Alternatives: Falling through to agent 1629, trusting stale chat memory, or reading neighboring prompts into the 1604 decision space.
Scalability potential: Low = stable task isolation during active batch churn; Middle = fewer false restarts after context compression; High = same parser applies to all attributed prompt tags; Ultra = can become a shared prompt-extraction guard.
Hardware Impact: No runtime impact; avoids wasted editor/build work caused by wrong-domain execution.

## Decision 17 - Parser Without Project Build

Problem: Brace and grep checks prove structure, but the user explicitly requested static syntax validation without `dotnet build`.
Solution: Use parser-only Roslyn validation. `csi.exe` was unavailable in PATH/VS folders during the latest pass, so PowerShell loaded Unity's Roslyn assemblies and the exact `System.Runtime.CompilerServices.Unsafe` dependency, then parsed `FloraTopologyStudio1604.cs` in memory. Result: `ROSLYN_PARSE_OK diagnostics=0`. No project compile, MSBuild, Unity import, or `dotnet build` was launched.
Rejected Alternatives: Running `dotnet build` under CPU/compiler contention, using `dotnet exec` while another `dotnet` is active, or claiming AST proof from brace counting alone.
Scalability potential: Low = cheap syntax gate on weak machines; Middle = repeatable parser-only validation; High = can parse more 1604-owned editor files; Ultra = can evolve into an EditMode analyzer once compile state is clean.
Hardware Impact: Avoids multi-minute build churn on low-end/shared host machines; no long-lived parser process was started by 1604.

## Decision 18 - Kelp Ribbon Skin Instead Of Tube Lie

Problem: `KelpForestFrond` was generated through the same tube skin as coral and tube worms, contradicting the required ribbon/frond silhouette and wasting triangles on round cross-sections that do not buy player belief.
Solution: Route kelp edges through `AddRibbon`: two-edge strip vertices, double-sided triangles, deterministic edge wave, root-distance sway mask, and the same vertex-color phase/glow payload. Coral and tube worms stay on tube skin with deterministic cellular/corrugation radius modulation.
Rejected Alternatives: Keeping cylindrical kelp, adding runtime cloth/physics, or adding a separate runtime mesh deformation system.
Scalability potential: Low = fewer kelp vertices and better far silhouette; Middle = ribbon strips remain compatible with LODGroup/GPU Resident Drawer; High = near kelp gets stronger authored material sway; Ultra = dense seed packs can add more fronds without CPU animation.
Hardware Impact: Low-end i3/MX350 gets cheaper kelp geometry and no per-frame CPU deformation; high-end gains more readable frond silhouettes with shader-only motion.

## Decision 19 - Fail-Closed MeshData Indices

Problem: `MeshUpdateFlags.DontValidateIndices` is correct for performance, but only if generated indices are proven valid before upload.
Solution: Add `ValidateIndexRange` after the Burst skin job and before MeshData creation. Corrupt index output now fails closed with an Editor error instead of relying on Unity validation or risking corrupt mesh data.
Rejected Alternatives: Removing `DontValidateIndices`, which pays validation cost on every generation path; trusting index math without a local proof gate.
Scalability potential: Low = safer authoring on weak machines; Middle = fast fail for bad genomes; High = index gate can be reused by future QEM/VAT bake paths; Ultra = large seed batches stop on exact invalid source, not later import damage.
Hardware Impact: Editor-only O(indexCount) pass; no runtime cost. Prevents wasted import/debug cycles from corrupt meshes.

## Decision 20 - Real Triangle Budget Contract

Problem: The editor field named `LOD0 Triangle Budget` was acting as node/edge capacity. That can silently emit meshes far above the intended scatter triangle budget.
Solution: Add `Lod0TriangleBudget` to `FloraGenomeDTO`, keep the runtime DTO 80 bytes with explicit `_pad0`, calculate edge capacity from triangles-per-edge, and reject any post-skin LOD that exceeds its derived budget.
Rejected Alternatives: Renaming the UI to hide the mismatch, trusting artists to infer real cost, or allowing high-poly coral through because generation is offline.
Scalability potential: Low = hard cap protects compact GPUs; Middle = larger authoring budgets remain predictable; High = near-field flora can spend more triangles intentionally; Ultra = dense seed packs still stay under explicit budget instead of accidental overflow.
Hardware Impact: Low-end i3/MX350 avoids over-budget LOD0 scatter assets; high-end visual overkill is still possible by raising one continuous budget value.

## Decision 21 - Hot GC And Blocking Verifier

Problem: APEX verification proved hot lookup and phase safety but did not explicitly reject GC/LINQ/string formatting or hidden blocking calls in `Execute` and direct Burst helper methods.
Solution: Extend `FloraApexIntegratorVerifier1604` with hot allocation, LINQ/string-format, `Complete`, wait, and `GetData` token scans, while still stripping literals/comments to avoid false positives.
Rejected Alternatives: Chat-only zero-GC claims, broad `new` bans that would falsely reject Burst value-type constructors, or building a full Roslyn analyzer during active compiler contention.
Scalability potential: Low = fast source gate before Unity import; Middle = catches common drift in flora/coral lanes; High = can become an EditMode gate after external compile blockers clear; Ultra = token set can be swapped for semantic analyzer later.
Hardware Impact: Prevents hot-path GC/blocking regressions before they hit compact CPUs; editor-only scan cost is negligible against avoided build/import stalls.

## Decision 22 - Runtime Layout Gate

Problem: Adding `Lod0TriangleBudget` to `FloraGenomeDTO` changed the serialized runtime-facing memory contract. Silent DTO drift on ARM64 would corrupt Burst jobs, generated assets, or future DataVault consumers.
Solution: Add `ValidateRuntimeLayouts` before mesh generation. It validates exact byte sizes and offsets for `FloraGenomeDTO`, `FloraNode`, `FloraEdge`, `FloraVertexData`, `FloraBoundsDTO`, and `FloraGenerationCounters`; the `UnsafeUtility.SizeOf<T>()` gate is constrained to `unmanaged`.
Rejected Alternatives: Trusting `[StructLayout(LayoutKind.Sequential)]` alone, using `Pack=1`, or discovering layout drift through Unity import failure.
Scalability potential: Low = fail before asset bake on weak authoring machines; Middle = stable mesh payloads; High = future VAT/DataVault consumers can reuse the same layout assumptions; Ultra = layout gate can become an EditMode validation rule once external compile blockers clear.
Hardware Impact: No runtime cost. Prevents corrupt asset/import cycles that waste minutes on low-end i3/MX350 class hosts.

## Decision 23 - Audit Budget Consistency

Problem: Seed pack, determinism test, and silhouette audit previously used stale test budgets instead of the real preset triangle budgets. That could pass audits while shipping a different density class.
Solution: Route all preset tests through `ResolveDefaultTriangleBudget`, then enforce per-LOD derived triangle caps after skinning.
Rejected Alternatives: Separate "test" budgets that do not match generated assets, or a UI-only budget field without enforcement.
Scalability potential: Low = compact devices get true budgeted LODs; Middle = predictable generated density; High = stronger near-field flora can be requested explicitly; Ultra = large seed batches scale through the same continuous budget value.
Hardware Impact: Low-end i3/MX350 avoids accidental over-budget scatter prefabs; high-end visual overkill remains opt-in through the same authoring budget.

## Decision 24 - Silhouette Preservation Over Radius Shrink

Problem: LOD1/LOD2 were reducing ribbon width and tube radius after already reducing radial and axial topology. That risks failing the 10% silhouette audit and makes cheap LODs visually smaller instead of merely cheaper.
Solution: Preserve ribbon width and tube radius at all LODs. LOD cost reduction now comes from fewer axial/radial subdivisions while outer branch endpoints and cross-section scale stay stable.
Rejected Alternatives: Shrinking kelp width, shrinking coral/tube radius, or hiding silhouette loss behind lower triangle counts.
Scalability potential: Low = cheaper topology without visible plant collapse; Middle = stable dither/crossfade LODs; High = near-field detail still comes from LOD0 subdivisions and baked surface modulation; Ultra = future QEM can preserve the same external envelope.
Hardware Impact: Low-end i3/MX350 avoids popping/shrinking scatter fields while still drawing fewer vertices in far LODs; high-end keeps stronger near silhouettes.

## Decision 25 - Prefab Save Fail-Closed

Problem: `PrefabUtility.SaveAsPrefabAsset` can return null. Treating the call as always successful creates false proof: meshes may exist while the scatter-ready prefab does not.
Solution: `SavePrefab` now returns `bool`; `GenerateAndSave` returns false if prefab persistence fails and logs the exact path.
Rejected Alternatives: Assuming AssetDatabase persistence succeeded, or claiming generated prefab proof from mesh asset writes alone.
Scalability potential: Low = authoring failures stop before scatter integration; Middle = deterministic batch generation status; High = larger seed packs can report exact failed prefab path; Ultra = same check can feed a later EditMode validator.
Hardware Impact: No runtime cost. Avoids wasted scatter/debug cycles on missing prefab assets.

## Decision 26 - Material And Mesh Persistence Fail-Closed

Problem: A generated prefab with Unity's built-in default material is technically loadable but visually invalid for the abyssal flora lane. New mesh asset creation also returned the instantiated source mesh without proving the AssetDatabase could load it back.
Solution: Replace default-material fallback with `TryResolveMaterial`; missing required flora material now aborts generation. After `AssetDatabase.CreateAsset`, the generator reloads the mesh asset and aborts if the saved asset is missing.
Rejected Alternatives: Built-in default material fallback, silent material degradation, and trusting a void `CreateAsset` call as disk proof.
Scalability potential: Low = compact lane receives correct shared material families; Middle = generated prefabs bind to production shader inputs; High = richer material response can be driven by vertex color masks; Ultra = future VAT/material variants can fail closed through the same resolver.
Hardware Impact: No runtime cost. Prevents wrong-material scatter batches and missing mesh references from reaching placement/debug cycles.

## Decision 27 - Shader Family Contract And Failed Mesh Cleanup

Problem: A material can exist at the required path but still use a wrong shader family, and a failed `AssetDatabase.LoadAssetAtPath<Mesh>` after `CreateAsset` can leave a newly-created broken asset or in-memory mesh behind.
Solution: `TryResolveMaterial` now validates kelp materials against `Hecton8/Flora/KelpMaster` or `GPUInstancer/Hecton8/Flora/KelpMaster`, and coral/tube-worm materials against `Hecton8/Flora/CoralMaster` or `GPUInstancer/Hecton8/Flora/CoralMaster`. New mesh persistence now deletes a failed just-created asset path and destroys non-persistent in-memory mesh residue.
Rejected Alternatives: Accepting any material at the path, silently using a mismatched shader, or leaving failed authoring residue for later scatter/debug steps.
Scalability potential: Low = correct cheap shared material on compact devices; Middle = stable shader property contract for vertex colors; High = GPUI material family can consume richer masks; Ultra = future VAT material variants can fail closed through the same family gate.
Hardware Impact: No runtime cost. Prevents wrong-shader scatter batches and broken mesh assets from reaching low-end validation loops; estimated 10000000+ us saved per avoided bad batch.

## Decision 28 - Seed Pack Batch Failure Signal

Problem: The seed-pack menu invoked three `GenerateAndSave` calls but did not aggregate their return values. A single failed preset could still be followed by final `SaveAssets`/`Refresh`, making the batch look finished even when a prefab lane failed.
Solution: Aggregate all three generation results and return with an Editor error before final refresh/success logging if any preset fails. Individual successful entries remain valid assets; failed new mesh assets are already cleaned by the lower-level fail-closed path.
Rejected Alternatives: Fail-fast after the first preset, which hides additional bad presets in the same batch; or accepting partial batch success as a green build signal.
Scalability potential: Low = compact seed pack does not ship missing LOD families; Middle = artists get exact failing preset logs; High = larger seed packs can reuse the same aggregate gate; Ultra = batch validator can later promote this gate into CI once Unity compile state is clean.
Hardware Impact: No runtime cost. Avoids repeated scatter/import validation loops after partial authoring failure; estimated 10000000-30000000 us saved per bad seed-pack run caught immediately.
