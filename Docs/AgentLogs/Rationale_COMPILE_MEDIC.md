# COMPILE_MEDIC Rationale

## 2026-05-27 Initial Route

Problem: User requested repair of latest dotnet compile errors, warnings, and related contract problems. No compile-medic XML assignment exists in current batch prompt.
Solution: Use operational ID `COMPILE_MEDIC`; treat compile logs as primary evidence; confine edits to proven defect clusters after reading affected code and contracts.
Rejected Alternatives: Running immediate full `dotnet build` was rejected because AGENTS forbids build spam and requires using recent logs first. Suppressing warnings was rejected because LTS mandate treats warnings as debt unless architect-approved.
Scalability potential: Low tier receives lower editor/build churn and no hot-path allocation regressions; middle/high/ultra retain architecture correctness without broad rewrites.
Hardware Impact: Avoiding unnecessary full rebuilds preserves host CPU for concurrent agents. Runtime impact unknown until affected code is identified.

## 2026-05-27 First-Party Compile Cluster

Problem: Fresh `BUILD_COMPILE_MEDIC_RECHECK_20260527.log` showed real Hecton errors mixed with vendor/generated graph failures: Unity namespace shadowing in `BootstrapStatus`, duplicate private helpers across partial classes, missing Odin/ToolKinematics references, and `BufferID` errors caused by stale `Library/ScriptAssemblies/Hecton8.Core.Memory.dll`.
Solution: Fully qualify UnityEngine `Time`/`Physics`; keep one owner for `OffsetOf`, `TriangleWaveSigned`, and permanent-echo invalidation; route Hecton8.Core CLI compile to current memory source for `BufferID/IDataVault/GlobalDataVault`; add actual existing DLL references for Odin Attributes and ToolKinematics Contracts.
Rejected Alternatives: Dummy `BufferID` extension was impossible and would corrupt data sovereignty. Attribute shim classes were rejected because real Sirenix attributes exist under `Assets/Plugins/Sirenix/Assemblies`. Editing third-party vendor source was rejected until first-party and project-graph faults are separated.
Scalability potential: Low tier gets the same current vault IDs as high tier instead of stale binary layout; middle/high/ultra keep visual/system features that consume new BufferIDs without fallback amputations.
Hardware Impact: Runtime microseconds saved: 0 claimed. Compile-path gain is avoiding stale-reference cascades; runtime impact is neutral because source contracts match intended Unity assembly surface.

## 2026-05-27 Core Residual Contract Cluster

Problem: `BUILD_COMPILE_MEDIC_CORE_20260527_2.log` exposed remaining source-level contract drift: private DTO padding writes, stale `PoolSlotData.AupCell`, C# 14 `field` keyword conflict, ref-safety spans, static methods touching instance vault state, smoke testers using old job APIs, and hot/cold reference paths bypassing `GlobalRegistry`.
Solution: Initialized DTOs with `default` and stopped writing private padding; routed fauna pool position through existing explicit-layout `GridX/Y/Z`; renamed the C# 14 keyword local; parsed survival database spans immediately instead of returning spans across scopes; converted terrain seam helpers to instance methods; updated smoke testers to current voxel/contact contracts; resolved world resource spawner through `GlobalRegistry.WorldResourceSpawner`.
Rejected Alternatives: Adding an overlapping `AupCell` field to `PoolSlotData` was rejected because it would corrupt the explicit 72-byte ARM64 layout. Publicizing padding was rejected because padding is not a contract. Scene searches for ore spawner were rejected because registry already owns the cold dependency route.
Scalability potential: Low tier keeps cheap integer-grid AUP reads and avoids managed fallback allocations in hot paths; middle/high/ultra preserve richer vegetation/fauna/terrain telemetry because compile fixes do not amputate systems.
Hardware Impact: Runtime microseconds saved: 0 claimed. Risk reduction is compile-path and data-layout correctness; no new hot-path work was added.

## 2026-05-27 Core Green Verification

Problem: The residual Core build narrowed to compatibility-slice visibility after the main contract fixes; the partial `FaunaBrain.Compatibility.cs` is compiled in a CLI slice where the full runtime field set is not visible in nested contexts.
Solution: Used an internal static compatibility bridge cache with subsystem reset and GlobalRegistry-backed resolution. `Hecton8.Core.csproj` reached 0 errors in `BUILD_COMPILE_MEDIC_CORE_20260527_7.log`.
Rejected Alternatives: Reintroducing a direct scene search was rejected because it violates registry-route doctrine. Making the full fauna runtime field public was rejected because it expands API surface for a compile-slice artifact.
Scalability potential: All tiers keep the same vegetation threat source; cache is cold and reset-bound, no frame polling added.
Hardware Impact: Runtime microseconds saved: 0 claimed. Compile delta: Core errors reduced from 197 to 0 across focused passes.

## 2026-05-27 Vendor Project Graph Repair

Problem: Full solution build exposed 220 errors after Core was green. The dominant failures were generated CLI project graph drift: Unity version symbols stopped at `UNITY_6000_*`; MapMagic asmref folders were compiled by `Assembly-CSharp-Editor` or omitted from `MapMagic*.csproj`; Astar/MeshBaker/Candice local plugin DLLs were outside reference globs; URP consumed `Burst.Compiler.IL.dll`; Bakery/Amplify editor-only members were hidden behind missing editor defines.
Solution: Centralized CLI Unity version defines in `Directory.Build.*`; mapped MapMagic asmref folders into `MapMagic`, `MapMagic.Editor`, and `Den.Tools.Editor`; removed MapMagic source/reference bleed from `Assembly-CSharp*`; added explicit references for Astar Clipper/Ionic/Poly2Tri, MeshBakerLib/MeshBakerEditorLib, Mono.Data.Sqlite, and Microsoft.CSharp; pruned Burst.Compiler.IL from CLI references; guarded Bakery's `System.Media` beep behind `HECTON_DOTNET_CLI_BUILD`.
Rejected Alternatives: Editing MapMagic graph assets or generator logic was rejected because targeted errors were assembly topology, not graph semantics. Copying DLLs into `Assets/Plugins` was rejected because it creates false ownership. Keeping `Burst.Compiler.IL` with aliases was rejected because Unity runtime code expects `Unity.Burst.OptimizeFor` without alias annotations.
Scalability potential: Low/middle/high/ultra runtime behavior is unchanged; compile graph now mirrors Unity ownership so visual systems remain available instead of being amputated for CLI.
Hardware Impact: Runtime microseconds saved: 0 claimed. Build-error reduction verified locally on targeted projects: MapMagic runtime/editor, Den.Tools runtime/editor, Astar, MeshBakerCore, BakeryEditor, ShaderGraph, URP runtime, Assembly-CSharp, Amplify editor, EasySave3, Technie, and NiceVibrations editor all compile with 0 errors in focused logs.

## 2026-05-28 Residual Core and Vendor Warning Pass

Problem: Fresh 2026-05-28 Core logs exposed live errors in first-party compile slices while several scary underwater/submarine errors were stale: current source had already removed the obsolete DataVault/tiny-job route in underwater visuals and already owned submarine vault write helpers locally.
Solution: Fixed only the active compiler faults: made the combat vault generation handle call explicit with `EnsureGenerationHandle<T>`, preserved combat target/telemetry write-lock ownership, and kept brownout GPU upload as a direct unsafe copy to the mapped buffer instead of managed element assignment. `Hecton8.Core.csproj`, `Hecton8.Editor.csproj`, and all Assembly-CSharp variants now compile with 0 warnings and 0 errors.
Rejected Alternatives: Re-adding stale underwater `_biomeFogVault` state or submarine helper aliases was rejected because those errors no longer exist in current source. Adding dummy DataVault APIs was rejected because it would violate one-owner data sovereignty. Editing vendor Graphy source for an unused private field was rejected because generated vendor warning policy already owns that class of debt.
Scalability potential: Low tier keeps cheap local underwater fog math and no tiny same-frame jobs; middle/high/ultra retain the same visual path without extra authority routes. Brownout upload stays one fixed-size GPU DTO write across tiers.
Hardware Impact: Runtime microseconds saved: 0 claimed. Compile/runtime risk removed without adding hot polling, heap allocations, or dependency lookup.

## 2026-05-28 Full Target Matrix and MapMagic/Crest Audit

Problem: A monolithic `dotnet build Hecton8.slnx --no-restore -v:minimal /m:1 /p:UseSharedCompilation=false` stalled after `GPUInstancer.Editor` with no further log movement; relying on that log would leave the tail of the solution unproven. A later warning pass also found `Tayx.Graphy.Editor.csproj` leaking `Unity.Addressables.Editor.dll` and `Hecton8.Core.Content.Editor.dll`, causing `MSB3277` for `System.Net.Http`, plus vendor `CS0169`.
Solution: Built every project from `Hecton8.slnx` individually with `--no-restore -v:minimal /m:1 /p:UseSharedCompilation=false`, each into `Docs/Reports/BUILD_COMPILE_MEDIC_TARGET_*_VERIFY_20260528_*.log` or focused first-party logs. Result: 62 projects, missing=0, warnings=0, errors=0. Added `Tayx.Graphy` and `Tayx.Graphy.Editor` to the existing generated-vendor project policy so the same editor reference pruning and normal-build vendor warning handling applies. MapMagic runtime/editor and Crest runtime/editor C# projects compile 0/0; no current git diff exists under `Assets/MapMagic` or `Assets/Crest` after the parallel workspace settled.
Rejected Alternatives: Suppressing `MSB3277` globally was rejected because it would hide real framework conflicts. Editing generated `.csproj` files directly was rejected because Unity regenerates them. Treating the stalled full `.slnx` process as proof was rejected because it did not emit completion or summary.
Scalability potential: Low/middle/high/ultra runtime behavior is unchanged; this is compile graph correction only. Cheap devices and high-end devices keep the same MapMagic/Crest feature surface because no vendor runtime source was amputated.
Hardware Impact: Runtime microseconds saved: 0 claimed. Build diagnostics are now deterministic at project granularity; host CPU waste from repeating a hung aggregate build is avoided.

## 2026-05-28 Strict Core Duplicate-Type Audit

Problem: Strict `Hecton8.Core` audit (`HectonStrictWarningAudit=true`) exposed 760 `CS0436` warnings: Core compiled source-owned types while also referencing stale/generated DLLs for Bootstrap.Contracts, World.Contracts, BatteryChargerLogistics, UI.Navigation, Lighting, and Core.Database. Removing the mask exposed real source faults in XR brownout, flocking black-box dump path, localization hash typing, and Sargassum threat-grid vault upload.
Solution: Pruned those stale source-owned DLL references from the Core CLI graph, added the missing XR render namespace, restored the flocking dump path constant, cast the localization key hash to the expected unsigned policy key, and changed Sargassum threat-grid upload from an untyped byte view to a typed `NativeArray<uint>` vault write lock with release in `finally`. Core normal and strict builds now both report 0 warnings and 0 errors.
Rejected Alternatives: Suppressing `CS0436` was rejected because it leaves stale binary contracts in the compiler graph. Adding source aliases or wrapper shims was rejected because it creates false ownership. Keeping byte-level Sargassum upload was rejected because the handle is `VaultGenerationHandle<uint>` and the buffer stride is `sizeof(uint)`.
Scalability potential: Low tier keeps the same cheap threat-grid upload path without stale contract ambiguity; middle/high/ultra keep the same richer boid/flocking telemetry and brownout render path. No gameplay authority route changed.
Hardware Impact: Runtime microseconds saved: 0 claimed. The Sargassum fix may remove undefined upload behavior; expected frame-time delta is neutral because it preserves one linear upload over the same cell count.

## 2026-05-28 Editor and Vendor Warning Cleanup

Problem: After Core was strict-clean, `Hecton8.Editor` exposed Modding SDK regressions: a static starter-kit generator touched instance window state and `Repaint()`, `Environment.NewLine` resolved through the project namespace, and deserialization DTO fields emitted `CS0649`. `Assembly-CSharp` still had one Candice runtime `CS0169` from a private non-serialized dead field.
Solution: Made the starter-kit generator return its status string while the Hub instance owns `_lastValidatorSummary` and repainting; fully qualified `global::System.Environment.NewLine`; initialized JSON DTO fields to default values compatible with Unity serialization; deleted the unused Candice `projectiles` field.
Rejected Alternatives: Making `_lastValidatorSummary` static was rejected because it would couple windows and hide instance state. Global warning suppression for Candice was rejected because one dead field was the only warning. Editing MapMagic or Crest assets was rejected because no diff exists there and their C# projects already compile 0/0.
Scalability potential: Runtime tier behavior is unchanged. Editor tooling remains deterministic and avoids hidden shared state between Hub and Workbench windows.
Hardware Impact: Runtime microseconds saved: 0 claimed. Editor compile warning count reduced to zero; no hot-path work added.

## 2026-05-28 Post-Strict Proof Matrix

Problem: The strict Core fix changed project graph policy; first-party and vendor projects needed re-verification after the mask was removed.
Solution: Rebuilt every project listed in `Hecton8.slnx` individually with `--no-restore -v:minimal /m:1 /p:UseSharedCompilation=false`. Proof artifact: `Docs/Reports/BUILD_COMPILE_MEDIC_TARGET_POST_STRICT_20260528_1.csv`; result: 62 projects, missing=0, warnings=0, errors=0. MapMagic, MapMagic.Editor, Crest, and Crest.Helpers.Editor logs each report 0/0.
Rejected Alternatives: Trusting previous matrix logs was rejected because the Core graph changed. Running only the aggregate `.slnx` was rejected because the prior aggregate run stalled after `GPUInstancer.Editor`; per-project proof is deterministic and complete for compiler correctness.
Scalability potential: All hardware tiers keep existing runtime systems; no vendor runtime or water/terrain graph source was amputated to make the build pass.
Hardware Impact: Runtime microseconds saved: 0 claimed. Build proof cost was about 1,582,700,000 us on the host; no runtime cost introduced.

## 2026-05-28 XRPass Reference Route and GPUInstancer List Contract

Problem: A fresh strict/Core dependency run showed `HectonVRBrownoutFeature` still failing on `XRPass` in the CLI graph. The type exists in `Library/ScriptAssemblies/Unity.RenderPipelines.Core.Runtime.dll`, but the Core project relied on the broad `Library/ScriptAssemblies/*.dll` wildcard, which did not provide a stable SRP Core runtime reference in this route. A later `Hecton8.Editor` build exposed a separate current compile break in GPUInstancer: `GPUInstancerDetailCell.detailMapData` is `List<int[]>`, but the active guard used `.Length` on the list itself.
Solution: In `Directory.Build.targets`, remove the wildcard SRP Core runtime reference from the `Hecton8.Core` fallback graph and re-add `Unity.RenderPipelines.Core.Runtime` by explicit `HintPath`. In `GPUInstancerDetailManager`, change only the list bounds check to `r >= cell.detailMapData.Count`; keep per-layer `int[]` checks on `.Length`. Normal verification logs now show `Hecton8.Core`, `GPUInstancer`, `Hecton8.Editor`, `Assembly-CSharp`, `Assembly-CSharp-Editor`, MapMagic runtime/editor, and Crest runtime/editor at 0 warnings and 0 errors.
Rejected Alternatives: Adding a local `XRPass` shim was rejected because Unity package types must come from SRP Core ownership. Removing XR comfort eligibility from brownout was rejected because it would amputate a real VR path. Rewriting GPUInstancer detail streaming was rejected because the compile fault was a one-token container contract mismatch.
Scalability potential: Low tier keeps the same CPU/GPU terrain detail path and VR brownout fallback behavior; middle/high/ultra retain SRP/URP brownout and GPUInstancer detail streaming without degraded visual feature surface.
Hardware Impact: Runtime microseconds saved: 0 claimed. The fixes are compile graph and bounds-contract repairs only; expected frame-time delta is neutral.

## 2026-05-28 Strict Warning Audit Correction

Problem: The previous strict-clean note was superseded by a fresh `HectonStrictWarningAudit=true` run. That run now has 0 errors, so the `XRPass` blocker is fixed, but it exposes 904 existing warnings when normal project `NoWarn` policy is deliberately disabled. The set is dominated by Unity serialized-field `CS0649`, assigned-but-unused `CS0414`, obsolete Unity API `CS0618`, and remaining source/DLL duplicate-type `CS0436` audit debt.
Solution: Treat the strict log as an unsuppressed audit artifact, not the normal compile contract. Normal affected builds are 0/0. The remaining strict warning debt needs a dedicated policy pass because touching hundreds of serialized fields and ABI residue opportunistically would risk scene/prefab behavior.
Rejected Alternatives: Suppressing strict warnings to recreate a fake 0/0 was rejected. Mass-editing serialized fields during a compile-medic fix was rejected because it would create wide prefab/serialization churn without domain-owner review.
Scalability potential: Runtime tier behavior is unchanged. Normal builds remain warning-clean while strict audit debt is visible instead of hidden.
Hardware Impact: Runtime microseconds saved: 0 claimed. This is diagnostic classification only.

## 2026-05-28 Parallel Dirty Compile Recovery

Problem: New builds ran while other agents were editing and rebuilding. Fresh logs showed `ModuloSimulationBucketer.ReleaseRebalanceBufferPins` missing, but the current source already contained the method and the matching pin-held fields.
Solution: Waited for external `dotnet/csc` activity to finish, inspected the DataVault lock contract, and rebuilt `Hecton8.Core` from the settled tree. `BUILD_COMPILE_MEDIC_CORE_FINAL_AFTER_BUCKETER_20260528_1.log` proves 0 warnings and 0 errors.
Rejected Alternatives: Adding a duplicate helper or changing the method name was rejected because current source was already coherent. Killing the running solution build was rejected because it was real compiler work from a parallel route.
Scalability potential: Low/middle/high/ultra behavior is unchanged. The rebalance job keeps relocation pins only for scheduled job lifetime and does not add writer-lock contention.
Hardware Impact: Runtime microseconds saved: 0 claimed. This removed a stale compile signal only.

## 2026-05-28 Modular Equipment Ref Safety

Problem: `ModularEquipmentEngine` used a `ref` lock counter path with `EquipmentVaultView<T>` ref-struct views, producing C# ref-safety compiler failures.
Solution: The helper now only acquires and returns the write view; the caller increments `acquiredCount` after each successful acquisition and releases by counted buffer order on failure.
Rejected Alternatives: `scoped ref` propagation, unsafe counter storage, and warning suppression were rejected because they obscure ownership and still keep the ref-struct escape risk.
Scalability potential: All tiers keep the same equipment vault buffer surface. Failure unwinds remain deterministic and no extra heap allocation or registry polling is added.
Hardware Impact: Runtime microseconds saved: 0 claimed. The fix is compile/ref-safety correctness with neutral frame-time cost.

## 2026-05-28 Ballistics Ricochet Constant Ownership

Problem: `BallisticIntersectionJob` is namespace-level code, so it could not resolve an unqualified private `MaxRicochetsPerTrajectory` member from `BallisticsRuntime`.
Solution: Made the ricochet budget constant internal on the owner type and qualified the job access as `BallisticsRuntime.MaxRicochetsPerTrajectory`.
Rejected Alternatives: A magic literal inside the job and a new duplicated constant were rejected because one fact must have one owner.
Scalability potential: Ballistics keeps one authoritative ricochet cap across low/middle/high/ultra tiers. No gameplay authority or DTO layout changed.
Hardware Impact: Runtime microseconds saved: 0 claimed. The fix is compile ownership only.

## 2026-05-28 Crest and MapMagic Boundary Verification

Problem: The user specifically flagged manually edited MapMagic graphs and Crest water. Current workspace has no MapMagic or `02_HECTON_WORLD.unity` diff, but Crest has compute/C# safety edits around dispatch bounds, FFT resolution clamps, null shader/resource guards, and bake cleanup.
Solution: Rebuilt Crest runtime/editor and all MapMagic runtime/editor/settings/MicroSplat C# targets in `BUILD_COMPILE_MEDIC_TARGETED_FINAL_AFTER_BUCKETER_20260528_1.csv`; all are exit=0, warnings=0, errors=0. Static diff scan found no conflict markers or placeholder stubs in those paths.
Rejected Alternatives: Editing MapMagic graphs or scene YAML was rejected because there is no current diff and dotnet cannot validate Unity serialized graph semantics. Claiming shader safety as fully proven was rejected because compute shader import/runtime validation needs Unity Editor.
Scalability potential: Crest changes are continuous-boundary guards and dispatch sizing, not low/ultra binary switches. MapMagic feature surface is not amputated.
Hardware Impact: Runtime microseconds saved: 0 claimed. Crest guards may prevent out-of-bounds GPU writes on non-divisible resolutions; expected CPU cost is negligible relative to compute dispatch.

## 2026-05-28 Strict Audit Debt Classification

Problem: Current strict Core audit still reports 904 warnings when normal warning policy is disabled. Normal builds are warning-clean, but strict mode exposes Unity serialized-field, dead event, obsolete API, and source/DLL duplicate-type audit debt.
Solution: Recorded the strict log as `BUILD_COMPILE_MEDIC_CORE_STRICT_AUDIT_AFTER_BUCKETER_20260528_1.log`, with 0 errors. Did not mass-edit serialized fields or public event surfaces during compile-medic work.
Rejected Alternatives: Suppressing the strict audit or mass-initializing hundreds of serialized fields was rejected because it would either hide real debt or risk prefab/scene serialization churn without owner review.
Scalability potential: Runtime tier behavior is unchanged. This is a diagnostic debt ledger, not runtime path code.
Hardware Impact: Runtime microseconds saved: 0 claimed. Normal build warnings remain 0; strict warning cleanup remains a separate policy task.
