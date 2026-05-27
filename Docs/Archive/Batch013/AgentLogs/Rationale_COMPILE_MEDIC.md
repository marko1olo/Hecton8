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
