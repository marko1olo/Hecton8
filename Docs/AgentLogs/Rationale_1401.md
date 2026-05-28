# Rationale 1401

Date: 2026-05-28
Status: PENDING VERIFICATION

## Initial Decision - Evidence Before Patching

Problem: User requested APEX final verification, but agent 1401 has not modified vendor code in this session. Claiming completion would be false.

Solution: Create explicit status and rationale ledgers before code edits. Use static source/report scans as the first proof layer. Only run build tooling after CPU and compiler contention checks pass.

Rejected Alternatives: Running dotnet build immediately was rejected because project instructions ban blind compilation under contention and user explicitly warned that build blocks other systems. Installing obsolete Mono.Data.Sqlite DLLs was rejected because task requires namespace quarantine/redirection without outdated manual DLL dependency.

Scalability potential: Low tier remains unaffected by vendor editor and compile shims. Middle/High/Ultra benefit only through build hygiene and isolated vendor assemblies; no gameplay truth route changes.

Hardware Impact: 0 runtime us/frame expected. Compile-time isolation may reduce editor compile graph fanout, but no measured proof yet. MX350/i3 runtime impact is unchanged until profiler proof exists.

## Domain Boundary

Allowed edit zone: third-party/vendor folders, package compatibility shims, vendor asmdefs, and proof tooling/logs.

Forbidden without critical justification: first-party `Assets/_Project`, `Hecton8.Core`, simulation assemblies, project settings, Quality/URP assets, Tags/Layers.

## Verification Language

All runtime, GC, Unity import, and compile-health claims remain PENDING VERIFICATION until artifact paths are generated.

## Decision - Candice SQLite Default Quarantine

Problem: `CandiceSQLiteProvider.cs` directly imported `Mono.Data.Sqlite`, and stale 2026-05-27 logs prove it caused CS0234/CS0246 when the CLI compiler could not resolve the namespace. The same provider is runtime-reachable through Candice demo save code, so editor-only wrapping would be a false fix.

Solution: Keep the legacy provider behind explicit `CANDICE_LEGACY_MONO_SQLITE`; default build compiles a disabled provider that returns fail-safe values and reuses static empty lists. Disabled provider logs once only in Editor/Development builds. Candice legacy DLL plugin import is disabled by default in `.meta`.

Rejected Alternatives: Installing or force-referencing obsolete `Mono.Data.Sqlite.dll` was rejected because it keeps Assembly-CSharp coupled to a vendor DLL and does not scale to Android/macOS/Linux. Full SQLite emulation was rejected because no supported `Microsoft.Data.Sqlite` package exists in the project and native SQLite platform proof is absent.

Scalability potential: Low/Middle/High/Ultra runtime cost is flat: disabled provider does no SQL work and has no per-call list allocation. Legacy SQL is opt-in only and remains outside HECTON-approved save authority.

Hardware Impact: Estimated runtime impact on i3/MX350 is 0 us/frame for default disabled provider unless a caller invokes Candice save methods; invoked calls perform constant fail-safe returns after one development log. No profiler artifact exists.

## Decision - Vendor Assembly Isolation

Problem: Candice had no asmdef and therefore bled into `Assembly-CSharp`. Amplify and Technie had asmdefs but auto-reference defaults allowed vendor assemblies to become ambient dependencies.

Solution: Added Candice runtime/editor asmdefs with `autoReferenced: false`; set Amplify runtime/editor and Technie runtime `autoReferenced: false`; removed the root CLI reference injection for `Mono.Data.Sqlite`.

Rejected Alternatives: Keeping `Directory.Build.targets` as the primary SQLite fix was rejected because it hides the vendor dependency instead of repairing the source boundary. Moving vendor folders was rejected because it would churn asset GUIDs.

Scalability potential: Compile graph isolation scales better as packages grow. Runtime visual scalability is unchanged because no gameplay presentation route was edited.

Hardware Impact: Runtime 0 us/frame. Compile-time fanout should drop after Unity regenerates projects, but this is PENDING VERIFICATION.

## Decision - Build Resource Throttle

Problem: User explicitly forbade blind dotnet/MSBuild use and the project has active parallel agents.

Solution: `Tools/Run_Guarded_Vendor_Compile_1401.ps1 -DryRun` sampled CPU above the 50 percent gate and exited before build. Latest artifact is `Docs/AgentLogs/Build_1401_Attempt_20260528_034039_BLOCKED_BY_CONTENTION.json`, with CPU 100 percent and active `dotnet` process 34196. The script now serializes compiler process lists as JSON arrays.

Rejected Alternatives: Running a target build under contention was rejected. Treating stale build logs as current proof was rejected.

Scalability potential: Tooling does not affect game runtime. It protects shared workstation throughput.

Hardware Impact: Prevented additional compile load on already saturated CPU.

## Decision - Build Resource Throttle Recheck 2026-05-28 03:49+04

Problem: A final vendor compile attempt was requested, but the host was still saturated. Running `dotnet build` under that load would violate the explicit throttling rule.

Solution: Executed the guarded script in dry-run mode only. It wrote `Docs/AgentLogs/Build_1401_Attempt_20260528_034938_BLOCKED_BY_CONTENTION.json`: CPU 100 percent, active `dotnet` PID 1304, no build attempts.

Rejected Alternatives: Running vendor `.csproj` builds anyway was rejected. Treating the clean external compile-medic logs as my own build was rejected; they are external CLI evidence only.

Scalability potential: Tooling-only protection. Low/Middle/High/Ultra runtime routes unchanged.

Hardware Impact: Prevented additional compiler load on a CPU already at 100 percent.

## Decision - Technie Removed MeshCollider Inflation API

Problem: 2026-05-27 logs repeatedly showed Technie `RigidColliderCreator.cs` using removed `MeshCollider.inflateMesh` and `MeshCollider.skinWidth`. The old guard was `#if !UNITY_2018_4_OR_NEWER`, which is unsafe for CLI because Unity version defines may be absent outside the Editor compiler.

Solution: Replaced default property writes with `ApplyMeshColliderInflationCompatibility()`. Default path uses supported `Collider.contactOffset`, raising it to `Mathf.Max(Physics.defaultContactOffset, inflationAmount)` when inflation is enabled and resetting to `Physics.defaultContactOffset` when disabled. Removed properties remain only behind explicit `TECHNIE_LEGACY_MESHCOLLIDER_INFLATION`.

Rejected Alternatives: Leaving the negative Unity guard was rejected because it already failed in CLI logs. Deleting inflation state was rejected because hull authors may expect a tolerance effect.

Scalability potential: Low/Middle/High/Ultra all keep the same collider topology. The fallback is a cheap contact tolerance, not a mesh recook or physical simulation.

Hardware Impact: Estimated runtime cost is 0 us/frame; the code runs during collider materialization, not per-frame simulation. Official Unity API check: Unity 6 documents `Physics.defaultContactOffset`; older `MeshCollider.skinWidth` documentation marks the property obsolete and tied to `inflateMesh`.

## Decision - Amplify Shader Property API Drift

Problem: Documentation corpus contained Amplify CS0618 evidence for `ShaderUtil.GetPropertyCount(Shader)` and related property calls. These are editor-only, but strict warning gates still treat them as debt.

Solution: Replaced `ShaderUtil.GetProperty*` calls with `Shader.GetPropertyCount()`, `Shader.GetPropertyType()`, and `Shader.GetPropertyName()`, using `UnityEngine.Rendering.ShaderPropertyType.Texture` for the old `TexEnv` case.

Rejected Alternatives: Local `#pragma warning disable CS0618` was rejected because Unity 6 has a supported non-obsolete API with semantic parity.

Scalability potential: Editor material-copy behavior is unchanged. Runtime player builds are unaffected.

Hardware Impact: 0 runtime us/frame. Editor-only property scan cost remains proportional to shader property count.

## Decision - GPUInstancer Dispatch Type Drift

Problem: Fresh 2026-05-28 compile log `BUILD_COMPILE_MEDIC_EDITOR_WARNINGS_20260528_6.log` showed GPUInstancer CS1503 at `GPUInstancerDetailManager.cs(585,68)` and `(587,68)` because `Vector4.x/y` floats were passed to an int thread-group helper.

Solution: Added local `dispatchGroupsX/Y` integers using `Mathf.CeilToInt`, clamped to at least 1, then dispatched with those ints while preserving the vector data sent to the shader.

Rejected Alternatives: Casting inline was rejected because it would hide the minimum-one dispatch guard and duplicate conversion. Changing the helper to accept float was rejected because other callers correctly pass ints.

Scalability potential: Low-tier and high-tier dispatch math remains deterministic. No binary quality switch was added; existing compute load remains controlled by existing GPUInstancer settings.

Hardware Impact: Two scalar conversions per dispatch; estimated <1 us per dispatch on i3/MX350, 0 us/frame when not generating detail buffers.

## Decision - Vendor Proof Harness

Problem: Tasks 16 and 17 required runtime bridge and zero-GC evidence, but launching Unity tests or dotnet build was blocked by CPU/compiler contention.

Solution: Added isolated editor-only test assembly `VendorCompatibility.EditorTests` with no `Hecton8.Core` reference. Tests cover Candice disabled SQLite fail-closed behavior, Amplify mock mesh generation, and a Candice warm-loop allocation assertion using `ProfilerRecorder` plus `GC.GetAllocatedBytesForCurrentThread()`.

Rejected Alternatives: Claiming the tests passed without executing Unity was rejected. Placing tests under `Assets/_Project` was rejected because vendor proof should not create first-party dependency edges.

Scalability potential: Test assembly is editor-only and autoReferenced false. Runtime player cost is 0.

Hardware Impact: 0 runtime us/frame. Test execution cost is pending measurement.

## Decision - Candice Trigger Countdown Allocation

Problem: `TriggerNextScene.Update()` called `Mathf.RoundToInt(timer).ToString()` every frame while active, creating avoidable managed string allocation pressure in a vendor demo hot path.

Solution: Added a cold countdown string cache built in `Start()` and updated the UI text only when the displayed second changes.

Rejected Alternatives: Leaving the allocation was rejected because the fix is local and behavior-preserving. Replacing the UI system was rejected as vendor-demo scope creep.

Scalability potential: Low-tier avoids per-frame countdown string churn. Higher tiers see identical visuals.

Hardware Impact: Runtime saving is unmeasured; static proof shows 0 `.ToString()` calls in `Update()`. Profiler measurement remains pending.

## Decision - Directory.Build.targets Native Prune Warning

Problem: Fresh UTF-16LE build logs contained `MSB4130` warnings in `Directory.Build.targets` around native vendor reference pruning for Candice `sqlite3.dll` and Technie `VHACD`. The XML was valid but the OR chain was ambiguous to MSBuild.

Solution: Parenthesized the simple boolean operands in `HectonPruneNativeCliReferences` and verified `Directory.Build.targets` parses as XML.

Rejected Alternatives: Ignoring it was rejected because the user requires warning debt cleanup. Rewriting the whole target was rejected because other agents have active edits in the same file.

Scalability potential: Build-only change. No runtime route or quality tier route changed.

Hardware Impact: 0 runtime us/frame. Compile-time warning reduction is pending fresh build because CPU gate blocked compilation.

## Decision - First-Party Vendor Reference Classification

Problem: Source scan found first-party text matches for `AmplifyImpostor`, but they were comments, method names, or string-based `GetComponent` checks, not compile-time references. Reporting them as hard leakage would be false.

Solution: Updated `VendorStaticAudit_1401.json` and final report to separate hard Candice/Amplify/Technie compile references from soft string/comment mentions. Hard compile references remain 0. GPUInstancer references are intentional and already listed in first-party asmdefs.

Rejected Alternatives: Removing string/comment mentions was rejected because `ImpostorSystem` uses them as asset validation, not a compile edge. Treating GPUInstancer as quarantined like Candice was rejected because first-party world scatter systems deliberately depend on it.

Scalability potential: Keeps the intended GPUI route intact for dense flora/rock rendering across quality tiers.

Hardware Impact: 0 runtime us/frame; documentation/reporting correction only.

## Decision - Amplify Runtime Editor Symbol Leakage

Problem: Follow-up static audit found `Assets/AmplifyImpostors/Plugins/Scripts/Preferences.cs` importing `UnityEditor` from a runtime asmdef source file. The Preferences body was already editor-gated, but the unguarded import can still break player/runtime compilation when `UNITY_EDITOR` is absent. The same audit found `SpriteUtilityEx` keeping UnityEditor reflection text outside a guard; not a compile edge, but a brittle player fallback if automatic mesh outline is accidentally called outside the Editor.

Solution: Wrapped the `using UnityEditor;` import in `Preferences.cs` with `#if UNITY_EDITOR`. Isolated `SpriteUtilityEx` reflection and `Type` cache to `UNITY_EDITOR`; the non-editor branch now reuses a static empty `Vector2[][]` fallback. Added `Docs/AgentLogs/VendorRuntimeEditorLeakage_1401.json`, which reports 0 unguarded editor-symbol findings across Candice, Amplify, Technie, and GPUInstancer runtime vendor folders.

Rejected Alternatives: Moving Preferences to the Editor folder was rejected because it could churn asset/package layout and was unnecessary for one import. Throwing `NotSupportedException` in non-editor `GenerateOutline` was rejected because vendor player code should fail closed, not crash. Allocating a fresh empty outline array per call was rejected because a static empty fallback is cheaper.

Scalability potential: Low/Middle/High/Ultra player runtime avoids UnityEditor binding. Automatic impostor baking remains editor-only; no runtime simulation, visual quality switch, or gameplay truth route changed.

Hardware Impact: Runtime cost is 0 us/frame for normal gameplay. If the non-editor fallback is invoked, it assigns an existing static empty array; no per-call managed allocation is introduced by the fallback.

## Decision - Candice Disabled SelectObject Ref Preservation

Problem: The disabled Candice SQLite branch assigned `obj = null` before returning `ProviderUnavailable`. The legacy branch leaves the caller-provided `ref` dictionary unchanged when no row is found, so the disabled branch was a needless null-poisoning risk.

Solution: Removed the null assignment. `SelectObject` now logs once in Editor/Development builds and returns `-1` without mutating caller state.

Rejected Alternatives: Clearing the caller dictionary was rejected because it would still mutate caller-owned state. Allocating a replacement dictionary was rejected because the provider is disabled and hot-path proof requires no per-call managed allocation.

Scalability potential: All tiers get deterministic fail-closed behavior. No quality route or gameplay truth owner was added.

Hardware Impact: 0 us/frame measured proof absent; static method range has 0 hot-path `new`, `string.Format`, `.ToString()`, LINQ, or `foreach`.

## Decision - TextureFormat Source Hits Classification

Problem: Raw source scan still finds `TextureFormat` text in vendor code. The task requested a hunt for legacy texture format debt, but blind conversion to `GraphicsFormat` would be wrong where Unity APIs still expose `TextureFormat` directly, such as `Texture2D.format` and texture encoder branches.

Solution: Left those paths unchanged because the latest external compile-medic logs show no CS0618 warnings, and the remaining hits are cold texture/render-target creation or encoding decisions, not current obsolete diagnostics.

Rejected Alternatives: Bulk replacing `TextureFormat` with `GraphicsFormat` was rejected because it can break API signatures and texture encoder logic without a compiler warning proving the need.

Scalability potential: No runtime visual route changed. Texture quality scaling remains owned by existing rendering/streaming systems, not this vendor bridge.

Hardware Impact: 0 runtime change.

## Decision - Vendor Test Harness Compile Blocker Recheck 2026-05-28 04:20+04

Problem: Fresh external full-log `Docs/Reports/BUILD_COMPILE_MEDIC_FULL_WARNINGS_20260528_4.log` reported CS0104 in `Assets/VendorCompatibilityTests/Editor/VendorBridgeEditModeTests.cs` at lines 67 and 70. The test also still asserted Candice disabled `SelectObject` null-poisoning after the source contract was changed to preserve caller `ref` state.

Solution: Qualified destruction calls as `UnityEngine.Object.DestroyImmediate(...)` and changed the assertion to `Assert.AreSame(originalRow, row)`.

Rejected Alternatives: Removing `using System` was rejected because the same test uses `GC.GetAllocatedBytesForCurrentThread()`. Reverting Candice ref preservation was rejected because mutating caller state in the disabled provider was the actual defect.

Scalability potential: Editor test assembly only. Low/Middle/High/Ultra player runtime unchanged.

Hardware Impact: 0 runtime us/frame.

## Decision - MasterAudio Feature-Gated Warning Hygiene

Problem: The same external full-log reported CS0169 in forbidden legacy vendor folder `Assets/Plugins/DarkTonic/MasterAudio`: `_triggerEnterTime`, `_triggerEnter2dTime`, and `_loadAddressableCoroutine`. These fields are only used when `PHY3D_ENABLED`, `PHY2D_ENABLED`, or `ADDRESSABLES_ENABLED` branches compile.

Solution: Moved those field declarations behind the same preprocessor symbols as their usage sites. This removes inactive-symbol warnings without approving MasterAudio as a runtime audio route.

Rejected Alternatives: Global `NoWarn` expansion was rejected because it hides unrelated warnings. Deleting MasterAudio was rejected because third-party purge/deletion was not assigned here and would require asset/meta deletion planning. Editing first-party audio routes was rejected as outside 1401.

Scalability potential: No gameplay route or visual/audio quality route changed. This is compile hygiene for a legacy package on disk.

Hardware Impact: 0 runtime us/frame. Active feature builds retain the same fields and behavior.

## Decision - Candice Disabled Log Warning Hygiene

Problem: Fresh external full-log reported CS0169 for `CandiceSQLiteProvider.s_loggedDisabled` because CLI compilation without `UNITY_EDITOR` or `DEVELOPMENT_BUILD` strips the only use inside `LogDisabledOnce()`.

Solution: Moved `DisabledMessage` and `s_loggedDisabled` behind `#if UNITY_EDITOR || DEVELOPMENT_BUILD`, matching the only code path that uses them.

Rejected Alternatives: Leaving the warning was rejected because Task 10 requires scoped vendor warning cleanup. Global warning suppression was rejected.

Scalability potential: Runtime release path remains fail-closed and silent. Development builds keep one-shot diagnostic logging.

Hardware Impact: 0 runtime us/frame.

## Decision - System.Net.Http MSB3277 Residual

Problem: Fresh external full-log also reports `MSB3277 System.Net.Http` conflict in generated `Assembly-CSharp-Editor` and `Assembly-CSharp-Editor-firstpass` projects.

Solution: Classified it as unresolved build graph/reference unification debt and left it untouched in 1401. It is not a Candice/Amplify/Technie/GPUInstancer source API drift defect, and suppressing it from 1401 would be a warning-mask, not a fix.

Rejected Alternatives: Extending `NoWarn`/`MSBuildWarningsAsMessages` to generated Assembly-CSharp editor projects was rejected because it would hide a real reference conflict. Forcing a System.Net.Http reference was rejected without a fresh build matrix because it could disturb Unity package editor assemblies.

Scalability potential: Build-only residual. No game runtime tier route changed.

Hardware Impact: 0 runtime us/frame.

## Decision - Build Resource Throttle Recheck 2026-05-28 04:13+04

Problem: A fresh compile attempt after source patches was needed, but host contention remained above the allowed threshold.

Solution: Ran only the guarded dry-run. `Docs/AgentLogs/Build_1401_Attempt_20260528_041330_BLOCKED_BY_CONTENTION.json` records CPU 100 percent and active `dotnet` PID 40436. No `dotnet build` or MSBuild execution was launched by 1401.

Rejected Alternatives: Running build anyway was rejected by the project compilation throttling rule.

Scalability potential: Tooling-only protection. Runtime routes unchanged.

Hardware Impact: Prevented additional compiler load on saturated CPU.

## Decision - TriggerNextScene Cold Allocation Proof Hygiene 2026-05-28 04:31+04

Problem: Additional mandate re-read found that my `TriggerNextScene` countdown cache used a cold `string[]` allocation without the canonical `// COLD ALLOC:` proof comment. The same touched file still used `gameObject.tag == "..."` comparisons in trigger/collision callbacks.

Solution: Added the canonical cold-allocation comment before the countdown label array and replaced the touched tag comparisons with `CompareTag`. The per-frame `Update()` range remains unchanged and has 0 hot-path `new`, `string.Format`, `.ToString()`, LINQ, and `foreach`.

Rejected Alternatives: Leaving the missing proof comment was rejected because the code was mine and the local cold allocation mandate is explicit. Rewriting the whole coroutine-based scene transition was rejected as vendor demo scope creep and not required for current compile/API bridge work.

Scalability potential: Low/Middle/High/Ultra player runtime behavior is unchanged. The cache still pays a cold allocation once and removes per-frame countdown string formatting; `CompareTag` avoids Unity tag string property access.

Hardware Impact: 0 measured proof. Static expected effect is removal of tag property access from two vendor callbacks and preservation of 0 hot-path allocation in `Update()`.

## Decision - Build Resource Throttle Recheck 2026-05-28 04:31+04

Problem: User repeated APEX verification after the 04:20 source-level patches. A real guarded vendor compile attempt was justified if and only if the host gate was free.

Solution: Ran `Tools/Run_Guarded_Vendor_Compile_1401.ps1` without `-DryRun`. The wrapper sampled CPU 100 percent and active `dotnet` PID 40436, wrote `Docs/AgentLogs/Build_1401_Attempt_20260528_043130_BLOCKED_BY_CONTENTION.json`, and exited before launching any `dotnet build`. The `attempts` array is empty.

Rejected Alternatives: Forcing a build under CPU 100 percent was rejected. Treating the stale external full-log as current proof was rejected because its 1401 line numbers no longer match current source.

Scalability potential: Tooling-only protection. Runtime tier routes unchanged.

Hardware Impact: Prevented additional compiler load on saturated CPU.

## Decision - MasterAudio Runtime Editor Guard and Quarantine 2026-05-28 04:57+04

Problem: MasterAudio remained an unisolated legacy vendor package under `Assets/Plugins`, so stale generated CLI projects can still include it through `Assets\Plugins\**\*.cs`. Follow-up source scan also found `SingletonScriptable.cs` importing `UnityEditor` from a runtime folder; the editor API use was guarded, but the import itself needed the same guard for player/runtime compile hygiene.

Solution: Wrapped `using UnityEditor;` in `SingletonScriptable.cs` with `#if UNITY_EDITOR`, added a canonical `// COLD ALLOC:` note for its static folder list, and added three source asmdefs with `autoReferenced: false`: `DarkTonic.MasterAudio.Runtime`, `DarkTonic.MasterAudio.Editor`, and `RelationsInspector.Editor`. RelationsInspector keeps an explicit editor-only reference to `DarkTonic.MasterAudio.Runtime` because `MasterAudioEventBackend.cs` is the only scanned external editor backend with direct MasterAudio type usage. Extended `Tools/Assert_Asmdef_Leakage_1401.py` to include MasterAudio; the regenerated `Docs/AgentLogs/AsmdefLeakage_1401.json` reports PASS with 0 findings.

Rejected Alternatives: Deleting MasterAudio was rejected because package removal and asset/meta deletion were not assigned and could break editor-only tooling. Leaving it in firstpass was rejected because it contradicts vendor quarantine. Adding first-party references was rejected because `Assets/_Project` has 0 hard MasterAudio references. Forcing generated `.csproj` edits was rejected because Unity owns those files and will regenerate them.

Scalability potential: No gameplay/audio route or quality scalar route changed. Low/Middle/High/Ultra runtime behavior is unchanged; the benefit is dependency isolation and avoiding player-side UnityEditor binding.

Hardware Impact: Runtime 0 us/frame. Compile graph fanout should improve after Unity import/regeneration, but that remains PENDING because no Unity import or compile completed.

## Decision - Candice Shared Empty List Residual 2026-05-28 04:57+04

Problem: The disabled Candice SQLite provider returns static empty `List<T>` caches to stay allocation-free. Because the legacy API returns mutable `List<T>`, an external consumer could mutate the returned cache.

Solution: Kept the cached lists because the only shipped runtime caller found in `CandiceSaveManager.GetWeapons()` enumerates `SelectAll()` and does not mutate the provider list; allocating a fresh list per call would violate the Zero-GC goal for the disabled bridge. Documented the mutable-cache behavior as an API-level residual in the final report.

Rejected Alternatives: Returning a fresh `List<T>` was rejected as a managed allocation in the bridge. Changing the public return type to `IReadOnlyList<T>` or array was rejected because it breaks the vendor API contract. Adding a custom immutable `List<T>` subtype was rejected as extra managed complexity for a quarantined disabled provider.

Scalability potential: All device tiers keep constant fail-closed behavior; no SQL work or gameplay truth route is introduced.

Hardware Impact: 0 runtime allocation in the disabled provider hot methods by static scan; profiler proof remains pending.

## Decision - Build Resource Throttle Recheck 2026-05-28 04:57+04

Problem: After MasterAudio quarantine source patches, a final compile/resource gate check was justified, but only if CPU and compiler contention were below the project threshold.

Solution: Ran `Tools/Run_Guarded_Vendor_Compile_1401.ps1` without `-DryRun`. The wrapper sampled CPU 100 percent and active `dotnet` PID 59296, wrote `Docs/AgentLogs/Build_1401_Attempt_20260528_045742_BLOCKED_BY_CONTENTION.json`, and exited before launching any `dotnet build`. The `attempts` array is empty.

Rejected Alternatives: Forcing compilation under CPU 100 percent was rejected. Treating source asmdefs as compile proof was rejected because generated `.csproj` files are stale until Unity imports/regenerates them.

Scalability potential: Tooling-only protection. Runtime tier routes unchanged.

Hardware Impact: Prevented additional compiler load on saturated CPU.

## Decision - MasterAudio Example Assembly Coverage 2026-05-28 05:09+04

Problem: Follow-up audit found 11 MasterAudio example C# files under `Assets/Plugins/DarkTonic/MasterAudio/ExampleScenes/Scripts` outside the new runtime `Scripts` asmdef. Because `DarkTonic.MasterAudio.Runtime` is `autoReferenced: false`, those demo scripts could remain in firstpass/default assembly and lose access to MasterAudio runtime types after Unity imports the runtime asmdef.

Solution: Added `DarkTonic.MasterAudio.Examples.asmdef` in the example scripts folder with an explicit reference to `DarkTonic.MasterAudio.Runtime` and `autoReferenced: false`. Ran a source coverage scan: `MASTER_AUDIO_CSHARP_UNCOVERED_BY_SOURCE_ASMDEF=0`. Updated `Tools/Assert_Asmdef_Leakage_1401.py` so the examples asmdef is part of the PASS criteria.

Rejected Alternatives: Moving example scripts under runtime `Scripts` was rejected because it would churn vendor folder layout and scene references. Leaving examples in firstpass was rejected because it breaks quarantine. Deleting examples was rejected because asset removal was not assigned.

Scalability potential: No gameplay/audio route changed. This preserves demo isolation and prevents ambient dependency bleed on all hardware tiers.

Hardware Impact: Runtime 0 us/frame. Build graph impact is pending Unity regeneration proof.

## Decision - Guarded Compile Wrapper Coverage Expansion 2026-05-28 05:09+04

Problem: After adding MasterAudio asmdefs, the guarded compiler wrapper still targeted only Candice, Amplify, and Technie projects. A future green result from that wrapper would not prove the newly added MasterAudio/RelationsInspector assemblies.

Solution: Added `DarkTonic.MasterAudio.Runtime`, `DarkTonic.MasterAudio.Examples`, `DarkTonic.MasterAudio.Editor`, and `RelationsInspector.Editor` to `Tools/Run_Guarded_Vendor_Compile_1401.ps1`, and expanded the diagnostic filter to include `MasterAudio` and `RelationsInspector`. Static checks passed: PowerShell parser returned OK and Python `py_compile` returned OK.

Rejected Alternatives: Relying on stale generated `Assembly-CSharp-firstpass.csproj` was rejected because Unity has not regenerated source projects. Running an unguarded full build was rejected by CPU throttling policy.

Scalability potential: Tooling-only. Runtime routes unchanged.

Hardware Impact: 0 runtime us/frame. Future compile verification is broader and more honest.

## Decision - Build Resource Throttle Recheck 2026-05-28 05:09+04

Problem: After example assembly coverage and wrapper coverage updates, one final guarded compile attempt was justified only if the host was free.

Solution: Ran `Tools/Run_Guarded_Vendor_Compile_1401.ps1` without `-DryRun`. The wrapper sampled CPU 100 percent, active `csc` PID 27628, and active `dotnet` PID 55080, wrote `Docs/AgentLogs/Build_1401_Attempt_20260528_050920_BLOCKED_BY_CONTENTION.json`, and exited before launching any `dotnet build`. The `attempts` array is empty.

Rejected Alternatives: Forcing a build under active `csc` and CPU 100 percent was rejected.

Scalability potential: Tooling-only protection. Runtime tier routes unchanged.

Hardware Impact: Prevented additional compiler load on saturated CPU.

## Decision - Build Resource Throttle Recheck 2026-05-28 05:23+04

Problem: After the MasterAudio settings patch, a compile check was justified only if the host was free.

Solution: Ran `Tools/Run_Guarded_Vendor_Compile_1401.ps1` without `-DryRun`. The wrapper sampled CPU 100 percent and active `dotnet` PID 55080, wrote `Docs/AgentLogs/Build_1401_Attempt_20260528_052348_BLOCKED_BY_CONTENTION.json`, and exited before launching any `dotnet build`. The `attempts` array is empty.

Rejected Alternatives: Forcing compilation under CPU 100 percent was rejected.

Scalability potential: Tooling-only protection. Runtime tier routes unchanged.

Hardware Impact: Prevented additional compiler load on saturated CPU.

## Decision - Candice PluginImporter Static Quarantine Proof 2026-05-28 05:26+04

Problem: The C# bridge proved `Mono.Data.Sqlite` was behind `CANDICE_LEGACY_MONO_SQLITE`, but that alone did not prove Unity's PluginImporter would ignore the legacy managed/native SQLite binaries.

Solution: Added `Docs/AgentLogs/CandicePluginImporterAudit_1401.json`. It checks `Mono.Data.Sqlite.dll.meta` and `sqlite3.dll.meta`: both have `enabledOneCount=0`, `isPreloaded=0`, and `isExplicitlyReferenced=0`. It also records that generated `.csproj`/targets text has 0 `Mono.Data.Sqlite` references and that default-branch Candice provider compile references remain 0.

Rejected Alternatives: Treating raw `.meta` eyeballing as sufficient proof was rejected. Deleting DLL/meta files was rejected because asset deletion/removal was not assigned and would require package cleanup planning.

Scalability potential: All tiers keep the same fail-closed Candice path. No runtime SQL, native plugin load, DataVault route, or quality branch was introduced.

Hardware Impact: 0 runtime us/frame expected; Unity import proof remains PENDING.

## Decision - MasterAudio Settings Static Constructor Guard 2026-05-28 05:26+04

Problem: `MasterAudioSettings` still executed editor singleton setup as a runtime-source static constructor: cold `string.Format` plus `new List<string>` for asset folder creation. It was cold, but it was not needed outside the Editor.

Solution: Moved the static constructor behind `#if UNITY_EDITOR`, replaced `string.Format("{0}/{1}", AssetFolder, AssetName)` with `AssetFolder + "/" + AssetName`, and made the folder list capacity explicit: `new List<string>(2)` with a canonical `COLD ALLOC` comment. Also moved `SingletonScriptable`'s `List<string>`/`System.IO` imports and folder staging field fully inside `UNITY_EDITOR`, with `new List<string>(0)`.

Rejected Alternatives: Leaving it as a cold runtime allocation was rejected because the touched vendor file can be cleaner without behavior change. Deleting MasterAudio settings was rejected because package removal is outside 1401 and could break editor tooling.

Scalability potential: Low/Middle/High/Ultra runtime no longer pays this static editor setup if the type is touched in player code. No gameplay/audio route or `GlobalQualityWeight` branch changed.

Hardware Impact: Measured proof absent. Static effect is removal of one cold runtime-source `string.Format` and confinement of one editor-only `List<string>(2)` allocation to Editor compilation.

## Decision - Technie Auto-Collider Marker Allocation Cleanup 2026-05-28 05:59+04

Problem: Final hot-path evidence contained a noisy cold `List<RigidColliderCreatorChild>` allocation plus two `ToArray()` calls inside Technie collider materialization. This is not a per-frame path, but it weakened the static proof artifact and kept avoidable editor/runtime collider-bake churn.

Solution: Replaced the temporary list and `ToArray()` calls with two explicitly sized persisted arrays: `MeshCollider[autoColliders.Count]` and `RigidColliderCreatorChild[autoColliders.Count]`, both marked with canonical `COLD ALLOC` comments. The arrays are required because `HullMapping` stores array fields. No gameplay authority route changed.

Rejected Alternatives: Leaving the temporary list was rejected because the fix is local and eliminates an avoidable allocation. Reusing a static scratch array was rejected because the mapping needs stable per-hull ownership, not shared mutable scratch. Rewriting Technie collider generation was rejected as package-scope overreach.

Scalability potential: Low/Middle/High/Ultra runtime frame behavior is unchanged. Collider bake/materialization has one less temporary managed list and no `ToArray()` copy churn.

Hardware Impact: Measured proof absent. Static delta removes one cold temporary `List<T>` allocation and two cold array-copy calls from the patched Technie range.

## Decision - GPUInstancer Job Struct Audit 2026-05-28 05:59+04

Problem: A text-only `new` scanner flagged `new AutoUpdateTransformsJob()` in `GPUInstancerPrefabManager.Update()` as a possible hot reference allocation.

Solution: Verified source definition `Assets/GPUInstancer/Scripts/Core/DataModel/GPUInstancerRuntimeData.cs:505`: `public struct AutoUpdateTransformsJob : IJobParallelForTransform`. Regenerated the hot-path scan to classify it as `valueTypeNewCount=1`, not `hotNewReferenceTypeCount`.

Rejected Alternatives: Removing the job schedule was rejected because it is existing GPUI behavior and the real residual is same-frame `dependentJob.Complete()`/`SetData`, which needs profiler and route review. Calling it a reference allocation was rejected because it is mathematically false at the C# type level.

Scalability potential: No quality scalar or gameplay route changed. The remaining GPUI synchronization/upload residual is documented for a separate GPU bandwidth/job phase owner.

Hardware Impact: 0 measured runtime proof. Static proof now separates value-type job construction from managed reference allocation.

## Decision - Build Resource Throttle Recheck 2026-05-28 05:49+04

Problem: A final guarded compile attempt remained necessary for evidence, but the machine was still saturated.

Solution: Ran `Tools/Run_Guarded_Vendor_Compile_1401.ps1` without `-DryRun`. The wrapper sampled CPU 100 percent and active `dotnet` PID 66408, wrote `Docs/AgentLogs/Build_1401_Attempt_20260528_054921_BLOCKED_BY_CONTENTION.json`, and exited before launching `dotnet build`. The `attempts` array is empty.

Rejected Alternatives: Forcing compilation under CPU 100 percent was rejected by the compilation resource throttling rule. Treating static JSON as compile proof was rejected.

Scalability potential: Tooling-only protection. Runtime tier routes unchanged.

Hardware Impact: Prevented additional compiler load on a saturated host.

## Decision - GPUI GLES3 Append Texture Reuse Guard 2026-05-28 06:18+04

Problem: Follow-up audit found `GPUInstancerUtility.SetAppendBuffersGLES3()` comparing `RenderTexture.width` to `runtimeData.bufferSize`. For buffers wider than `GPUInstancerConstants.TEXTURE_MAX_SIZE`, the allocated texture width is capped and height carries the extra rows, so the old width check could force repeated release/recreate churn. The shadow append branch also released and recreated its buffer/texture every call.

Solution: Added local `textureWidth`, `lodTextureHeight`, and `matrixTextureHeight` values derived from `rowCount`. Reuse checks now compare width and height against the actual allocation dimensions. Shadow append buffer recreation is guarded by null/count mismatch, and shadow append texture recreation is guarded by null/width/height mismatch.

Rejected Alternatives: Increasing `TEXTURE_MAX_SIZE` was rejected because it changes GPU texture capacity assumptions and does not solve the incorrect dimension predicate. Moving GPUI append data into first-party `GlobalDataVault` was rejected as domain overreach and wrong ownership; GPUI owns this GPU upload bridge. Adding a binary low-end branch was rejected because the defect is correctness/resource reuse, not quality policy.

Scalability potential: Low tier avoids unnecessary allocation/upload object churn when large instance buffers exceed one texture row. Middle/High/Ultra keep the same visual path and can spend saved CPU/GPU-driver time on existing GPUI density/crossfade work. No `GlobalQualityWeight` route was introduced because this patch does not decide fidelity; it repairs resource lifetime.

Hardware Impact: Measured profiler proof is absent. Static expected impact is fewer `RenderTexture`/`ComputeBuffer` release-create cycles in GPUI GLES3 setup for large instance buffers. Frame impact remains unproven until Unity/Profiler run.

## Decision - Guarded Compile Pass and Technie Warning Cleanup 2026-05-28 06:18+04

Problem: The first allowed guarded compile pass sampled CPU 42 percent and launched project builds. Amplify Runtime/Editor and Technie Runtime/Updater returned exitCode 0, but Technie Runtime emitted CS0169/CS0649 warnings: `TriangleBucket.averagedCenter`, `RigidColliderCreator.debugMesh`, and `SkinnedColliderEditorData.lastModifiedFrame`.

Solution: Removed the unused `averagedCenter` field, removed the unused compiled `debugMesh` field, and initialized `lastModifiedFrame` to 0 because the field is assigned on update and read through `GetLastModifiedFrame()`. The remaining `debugMesh` text is inside a commented-out debug block and is not a compiled field declaration. Static grep confirms only `private int lastModifiedFrame = 0;` remains among the warning field declarations.

Rejected Alternatives: Global warning suppression was rejected because the warnings were local and cheap to remove. Re-enabling the commented debug mesh field was rejected because it would preserve dead debug allocation/state in production source. Forcing a second compile after the patch was rejected because the post-patch gate sampled CPU 67 percent and active `dotnet` PID 53376.

Scalability potential: Warning cleanup is compile hygiene only. Low/Middle/High/Ultra runtime routes unchanged; no visual quality or gameplay truth route changed.

Hardware Impact: Runtime 0 us/frame expected. Post-patch compile proof remains pending; static brace and grep checks passed, but no Unity import or profiler proof exists.

## Decision - Compilation Resource Throttle Recheck 2026-05-28 06:13+04

Problem: A second compile after the Technie warning cleanup was needed for proof, but running it under active compiler contention would violate the explicit project rule.

Solution: Ran the guarded wrapper without direct `dotnet build`. It wrote `Docs/AgentLogs/Build_1401_Attempt_20260528_061325_BLOCKED_BY_CONTENTION.json`: CPU 67 percent, active `dotnet` PID 53376, `blockedByContention=true`, and an empty `attempts` array. No post-patch build launched.

Rejected Alternatives: Forcing compilation above the 50 percent CPU gate was rejected. Reporting the pre-patch Technie compile as post-patch proof was rejected because the warning cleanup occurred after that compile.

Scalability potential: Tooling-only protection. Runtime tier routes unchanged.

Hardware Impact: Prevented additional compiler load while the host was above the allowed CPU threshold.

## Decision - Guarded Compile Wrapper Single-Process Gate Fix 2026-05-28 12:36+04

Problem: The `Docs/AgentLogs/Build_1401_Attempt_20260528_061044_SUMMARY.json` artifact recorded one active `dotnet` process but still had `blockedByContention=false`. Root cause: PowerShell can unwrap a single returned object from `Get-HectonCompilerProcesses`; `$compilers.Count` is then not a reliable array count for the one-process case.

Solution: Forced `$compilers = @(Get-HectonCompilerProcesses)`, changed the gate to `$compilers.Length -gt 0`, blocked unavailable CPU samples (`$cpu -lt 0`), and added `compilerProcessCount` plus `blockReasons` to the JSON summary. Re-ran only the guarded wrapper. The fixed wrapper wrote `Docs/AgentLogs/Build_1401_Attempt_20260528_123346_BLOCKED_BY_CONTENTION.json`: CPU sample `-1`, active `csc` PID 33212, active `dotnet` PID 26840, `compilerProcessCount=2`, `blockReasons=[CPU_SAMPLE_UNAVAILABLE, ACTIVE_COMPILER_PROCESS]`, attempts array empty.

Rejected Alternatives: Treating the 06:10 compile as compliant proof was rejected because the artifact contradicts the throttling rule. Removing process detection was rejected. Running direct `dotnet build` after the tool fix was rejected because the fixed gate blocked.

Scalability potential: Tooling-only protection. Runtime low/middle/high/ultra routes unchanged. This preserves workstation throughput for parallel agents and prevents accidental build load on weak CPUs.

Hardware Impact: Prevented a build under active compiler contention. No runtime frame impact.
