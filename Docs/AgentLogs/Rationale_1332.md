# Agent 1332 Rationale

Problem: Agent status/rationale files were absent at session start.
Solution: Create status and rationale artifacts before source edits. This satisfies state-machine persistence and keeps compressed chat from becoming the authority.
Rejected Alternatives: Chat-only progress. It violates batch reporting and anti-amnesia protocol.
Scalability potential: Low/Middle/High/Ultra unaffected; this is process state, not runtime.
Hardware Impact: 0 us runtime impact on i3/MX350; editor filesystem write only.

Problem: Task domain is narrow but touches input, UI, file I/O, rendering presentation, and telemetry.
Solution: Bound first pass to `Hecton8.Input` assembly and `Assets/_Project/Scripts/UI/PauseControlsPanel.cs`, `Assets/_Project/Scripts/UI/PDAControlsRebindUI.cs`; expand only for proven interface/validator/shader hooks.
Rejected Alternatives: Whole-repo refactor or direct global registry surface invention. Both raise merge conflict and authority-route risk with 20+ agents.
Scalability potential: Low uses cheapest parser/UI path; Middle adds static tests; High/Ultra may spend saved CPU on richer presentation filters in VISUAL_SYNC only.
Hardware Impact: Expected gain is removal of UI-open delegate/string allocations; measured proof absent until profiler/GCMonitor.

Problem: `controls.json` requirement asks zero managed allocation but .NET/Unity file APIs have unavoidable managed boundary costs in cold I/O.
Solution: Treat disk read/write as cold menu/settings operation, use bounded byte buffers and manual ASCII/UTF-8 parsing, document any unavoidable managed framework allocation as cold-only and outside hot path.
Rejected Alternatives: `JsonUtility`, `Newtonsoft.Json`, `InputActionRebindingExtensions.SaveBindingOverridesAsJson`, or `StringBuilder`. They allocate managed strings/objects and hide reflection/boxing.
Scalability potential: Low/Middle/High/Ultra share identical save layout; quality weight must not mutate save identity or DTO layout.
Hardware Impact: Main gain is avoiding menu-frame heap churn and pause stutter on i3/MX350; exact microseconds pending static/profiler proof.

Problem: First scan found no `ControlRemapper.cs` or `AccessibilitySettings.cs`; current rebind owner persisted `controls.json` through Unity JSON strings and delete-then-move file replacement.
Solution: Add owner-local input remap backend and accessibility VISUAL_SYNC bridge without mutating public input interfaces. APEX reaudit removed the legacy Unity JSON/PlayerPrefs fallback from the active service because it created a managed string escape hatch.
Rejected Alternatives: Keeping `File.ReadAllText`, `LoadBindingOverridesFromJson`, or managed path decoding as a compatibility rescue. Compatibility is lower priority than the explicit controls.json zero-GC contract.
Scalability potential: Low uses scalar shader params and minimal parser work; Middle/High/Ultra use the same save identity while shaders can spend GlobalQualityWeight on stronger visual correction/blending.
Hardware Impact: Expected low-end gain is removal of File.ReadAllText/File.WriteAllText and Unity SaveBindingOverridesAsJson managed strings from the standard save/load path; exact i3/MX350 delta pending profiler.

Problem: Telemetry needs an exact 300-frame black-box route, but editing Core `BufferID` enum increases merge risk and public-surface churn.
Solution: Use owner-local numeric `BufferID` casts `70534` for ring and `70535` for cursor, owned by `SystemID.UI`, with 64-byte `InputBindingTelemetryEntry`. The IDs sit in the observed input/Babel gap.
Rejected Alternatives: Add enum members to `H8Memory.cs`, use ad-hoc high IDs, or use managed logs only. Enum expansion is public churn; high IDs were less defensible after static BufferID inspection; managed logs are not forensic state.
Scalability potential: Low can write only fault/success summaries; Middle/High/Ultra can sample additional duration/hash fields without changing DTO size or save identity.
Hardware Impact: 19.2 KB native ring plus one int cursor. Cold write target 1-4 us per entry on i3/MX350, pending measurement.

Problem: Accessibility filters were absent, and CPU-side color correction would violate the visual-sync presentation route.
Solution: Implement a 16-byte `AccessibilityConfigDTO` and upload through a double-buffered global CBuffer in `DispatcherPhase.VisualSync`.
Rejected Alternatives: Runtime `PostProcessVolume` allocation, material instance mutation, MPB on standard geometry, or CPU pixel arrays. All are either allocation-heavy or SRP-batcher hostile.
Scalability potential: Low strength/mode values feed a cheap branchless lerp; Middle adds stronger filter curves; High/Ultra can use the same scalar config to enable richer shader math without CPU work.
Hardware Impact: CPU upload is one 16-byte dirty CBuffer write, expected below 3 us on i3/MX350 after initialization; GPU cost belongs to shader pass and is quality-weighted.

Problem: `RebindingManager` used captured lambdas in `OnCancel`, `OnComplete`, and conflict callbacks.
Solution: Store the active rebind context in fields and route completion/cancel/conflict through cached method delegates initialized once in Awake/OnEnable.
Rejected Alternatives: Capturing local variables per rebind, static delegates without instance state, or changing the public conflict event contract. Captures allocate; static delegates cannot hold per-operation state; public API mutation is forbidden.
Scalability potential: Low/Middle/High/Ultra share identical UI event behavior; quality weight is irrelevant to input truth.
Hardware Impact: Expected gain is removal of per-rebind managed delegate allocations and the resulting menu-frame GC spikes on i3/MX350; exact bytes pending profiler.

Problem: Existing `controls.json` save path deleted the destination before moving the temp file, creating a crash window.
Solution: Write bounded ASCII JSON bytes to `.tmp`, flush, verify file length, then use `File.Replace` when destination exists and `File.Move` for first write.
Rejected Alternatives: `File.WriteAllText`, `StringBuilder`, Unity `SaveBindingOverridesAsJson`, or delete-then-move. They either allocate managed strings or lose atomicity.
Scalability potential: Low devices avoid heap churn; Middle/High/Ultra keep the same save identity and do not spend extra CPU for cosmetic behavior.
Hardware Impact: Cold save cost is storage-bound. The expected gain is GC avoidance, not raw disk speed; measured microseconds pending.

Problem: A malformed controls file could clear current overrides before all records were known to be applicable.
Solution: Parse into fixed DTO records, validate every action/binding/path against the current runtime, and only then call `ClearBindingOverrides`.
Rejected Alternatives: Apply while parsing or clear before validation. Both can leave partial input state after corrupted JSON.
Scalability potential: Low/Middle/High/Ultra all fail closed to current/default controls; no quality switch affects authority.
Hardware Impact: Adds one cold validation pass over at most 128 records; target below 100 us on i3/MX350, pending measurement.

Problem: The first parser revision decoded unsupported saved paths with `new string`, which violates the stricter controls.json zero-GC interpretation.
Solution: Remove the decode fallback entirely. `TryResolveExistingPathString` must match a Unity InputSystem-owned existing binding/override/effective path; otherwise `UnsupportedPath` telemetry is written and load returns false before clearing overrides.
Rejected Alternatives: Interning, string pools, or cold managed decoding. All still allocate or hide managed identity behind the input route.
Scalability potential: Low/Middle/High/Ultra keep identical deterministic input identity. Visual quality scaling remains isolated to accessibility shader math, not save semantics.
Hardware Impact: Saves managed heap pressure on low-end silicon when corrupted or stale controls files are encountered. Cost is compatibility: arbitrary path bytes from an old file are rejected rather than revived.

Problem: `ControlRemapIoResult` was an unmanaged result carrier without explicit layout, leaving one contract surface dependent on runtime packing.
Solution: Promote it to `[StructLayout(LayoutKind.Explicit, Size = 88)]`, placing the 64-byte telemetry payload at offset 0, then 4-byte result fields, then 4 bytes padding.
Rejected Alternatives: Leave it sequential because it is not stored in the Vault. That would weaken the layout proof and make the self-audit incomplete.
Scalability potential: Low/Middle/High/Ultra unaffected; this is a cold result carrier with stable binary shape.
Hardware Impact: No expected runtime delta. It removes ARM64 alignment ambiguity for editor tests and future native transport.

Problem: The fuzzer test used 64 iterations and did not prove the requested 10,000-save stress shape.
Solution: Increase `ControlsJsonSerializerSurvivesBoundedInputSpam` to 10,000 save requests and add `ControlsJsonRejectsUnknownPathWithoutClearingOverrides` to prove unsupported paths fail before mutation.
Rejected Alternatives: Leave the smaller loop and call it bounded. That was not the requested stress shape.
Scalability potential: Low/Middle/High/Ultra unaffected at runtime; test coverage raises confidence in cold settings I/O.
Hardware Impact: Editor-only cost. Runtime path unchanged.

Problem: Final compile proof after APEX edits is still required. Host CPU first sampled at 51.3%, then later fell to 43.8%.
Solution: Defer while CPU was above 50%, then launch one throttled `dotnet build .\Hecton8.Core.csproj --no-restore --disable-build-servers -p:UseSharedCompilation=false /m:1 -v:minimal -clp:ErrorsOnly` when CPU and process gates were clear. Build failed on unrelated World `HectonIndirectVegetationRenderer.cs` CS0246 `CpuCullingScratchBuffer` errors; no Agent 1332 errors emitted.
Rejected Alternatives: Build under load, edit World vegetation code outside domain, or claim a clean build. All violate explicit batch rules.
Scalability potential: Runtime unaffected; process avoids starving sibling agents.
Hardware Impact: Prevented compiler launch during overloaded host window, then used single-node/no-shared compiler settings to reduce host impact.

Problem: UI refresh still had cold/hot ambiguity from repeated PDA row hierarchy resolution and Pause button listener wiping.
Solution: PDA row references resolve once and cache the result; Pause buttons use `RemoveListener`/`AddListener` with cached `UnityAction` fields so inspector listeners survive.
Rejected Alternatives: Recurse hierarchy every `RefreshAll`, call `RemoveAllListeners`, or add anonymous listeners. Repeated search burns menu time; RemoveAllListeners breaks authored wiring; lambdas allocate.
Scalability potential: Low avoids menu hitch; Middle/High/Ultra can spend saved budget on richer PDA visuals without changing input state.
Hardware Impact: Expected saved 5-30 us during menu navigation/open on i3/MX350; exact profiler proof absent.

Problem: The black-box protocol requires a disk dump artifact, not only a native ring.
Solution: Add a cold fault-only dump path that writes the fixed 300-entry ring to `Docs/AgentLogs/Dump_1332.bin`.
Rejected Alternatives: Dump every save/load, Debug.Log strings, or hot-path file writes. Continuous dumping is I/O abuse; logs allocate and are not bounded; hot file writes are prohibited.
Scalability potential: Low writes only on faults; Middle/High/Ultra can inspect the same binary ring without extra runtime behavior.
Hardware Impact: Normal path 0 us. Fault path writes 19.2 KB plus filesystem overhead; cold diagnostic only.

Problem: Build verification is mandatory but the host was under heavy load, then the first permitted build failed outside my domain.
Solution: Run the CPU/dotnet gate before build. Gate initially reported 98.3% and 100% CPU, so build was deferred. When CPU fell to 16.8% and no dotnet/csc existed, one `dotnet build .\Hecton8.Core.csproj --no-restore` was launched. It failed on unrelated `Assets/_Project/Scripts/Gameplay/VRSomaticProvider.Comfort.cs` CS1654 errors. My generated-csproj duplicate include warnings were cleaned. A second build is blocked because another dotnet/csc command is active.
Rejected Alternatives: Launch build under >50% CPU, edit Gameplay code outside assigned domain, or claim a clean build. All violate explicit instructions.
Scalability potential: Runtime unaffected; process avoids starving sibling agents.
Hardware Impact: Prevented heavyweight compiler launches during saturated host load; one permitted build consumed ~60 seconds and exposed non-domain dependency errors.

Problem: APEX no-throw gate still saw broad `catch` forms in cold UI/InputSystem helper code and telemetry cleanup.
Solution: Remove UI/InputSystem broad catches by replacing exception recovery with bounds checks; narrow every remaining file I/O catch to `UnauthorizedAccessException` and `IOException`.
Rejected Alternatives: Keep broad catches because they were cold. The user requested a strict purge, and broad catches hide defects.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged. The failure model is simpler: invalid indexes fail closed by bounds checks, disk I/O returns false with telemetry.
Hardware Impact: 0 us hot-path cost; fewer exception filters in cold menu/file paths.

Problem: Final compiler proof was previously unavailable due unrelated compile blockers and host compiler contention.
Solution: Wait until no `dotnet`/`csc` process existed and CPU sampled at 14.1%, then run one throttled build with `/m:1`, disabled shared compilation, and errors-only output. Build succeeded: 0 warnings, 0 errors, 2.50 s.
Rejected Alternatives: Skip build after static scans or launch while another compiler existed. Both violate the batch and host-resource rules.
Scalability potential: Runtime unaffected; verified C# integration without starving sibling agents.
Hardware Impact: Single constrained build; no repeated compiler spam.

Problem: The save path still allocated a managed `FileInfo` object only to verify temp-file length.
Solution: Remove `new FileInfo(tempPath)` and verify bytes using `FileStream.Position` immediately after `Flush(true)`, before disposing the stream.
Rejected Alternatives: Keep FileInfo because the path is cold. The prompt targets the save click path; avoidable managed allocation is not acceptable.
Scalability potential: Low/Middle/High/Ultra save identity and failure behavior unchanged.
Hardware Impact: Removes one cold managed object allocation per successful controls.json save; hot path remains 0 us.

Problem: The previous hot-path proof used a brace/token scanner, not an independent syntax-tree parser.
Solution: Build and run a temporary net10 Roslyn hot-path scanner with build servers disabled. It parsed 7 touched C# files, found 4 hot methods, and reported 0 banned hits. Scanner hash: `4eb86ab54f66884e133e3575195d1119134755231c727f41bc7f02fc3d71ed3b`.
Rejected Alternatives: Rely on grep-only proof. The user requested syntax-tree-level verification.
Scalability potential: Runtime unaffected; verification quality improved.
Hardware Impact: Scanner build was gated by CPU/dotnet process checks; final project build remained single-node and green.

Problem: APEX re-audit found one remaining allocation class: Pause/PDA UI subscribed to input and rebinding events with method groups, creating delegate instances on each subscribe/unsubscribe cycle even though button UnityActions were cached.
Solution: Cache every `Action` delegate used by `PauseControlsPanel` and `PDAControlsRebindUI` in fields initialized from `Awake`/`Subscribe`, then subscribe/unsubscribe only those cached fields.
Rejected Alternatives: Treat method-group conversions as free, cache only button listeners, or rely on GC because this is menu code. Those leave avoidable heap churn when opening/rebinding controls.
Scalability potential: Low/Middle avoid pause-menu hitch from delegate churn; High/Ultra can spend the saved budget on richer PDA/accessibility presentation without changing input truth.
Hardware Impact: Expected gain on i3/MX350 is small per event but removes repeat managed delegate allocations across menu open/close and rebind cycles; exact bytes require Unity profiler proof.

Problem: The UI delegate patch invalidated the previous final build/hash proof.
Solution: Wait for CPU/dotnet/csc gates, then run one post-patch build: `dotnet build .\Hecton8.Core.csproj --no-restore --disable-build-servers -p:UseSharedCompilation=false /m:1 -v:minimal -clp:ErrorsOnly`. Result: 0 warnings, 0 errors, 96.40 s. Re-run the native field Roslyn audit after CPU returned below the threshold: 7 files, 0 fields, 0 persistent candidates.
Rejected Alternatives: Reuse stale build proof or launch scanners while CPU was above 50%. Both would be false verification.
Scalability potential: Runtime unaffected; verification now matches the current source hashes.
Hardware Impact: One constrained compiler pass and one fast Roslyn audit; no build spam.

Problem: User rejected the prior proof and requested a fresh self-audit from the original prompt.
Solution: Re-extracted `<AGENT_PROMPT id="1332">` from `Docs/Tasks/CURRENT_BATCH.md` with an attribute-tolerant regex, confirmed 20 tasks, reran native field Roslyn audit, broad syntax scanner, and focused hot-method Roslyn scanner. Focused hot-method result: 7 files, 4 hot methods, 0 hits, hash `703851db4de451fdf12e6adb4726807f81b113269bb891f7a2b10bcaf2893e2e`.
Rejected Alternatives: Reply from cached memory or treat the previous JSON as sufficient. The prompt explicitly demanded another disk-backed verification pass.
Scalability potential: Runtime unaffected; this strengthens proof artifacts without changing behavior.
Hardware Impact: Scanner runs were gated by CPU/dotnet state and completed without compiler spam.

Problem: Fresh build verification was temporarily blocked by non-domain World/Physics edits from parallel agents.
Solution: Do not patch outside the 1332 boundary. Wait for CPU and compiler gates, retry only when legal, and stop after the external wall clears. Final rerun succeeded: 0 warnings, 0 errors, 67.83 s.
Rejected Alternatives: Modify World vegetation/physics from the input/UI agent, claim stale build proof, or run repeated builds under CPU saturation.
Scalability potential: Runtime unaffected; build proof now reflects the current workspace after external fixes landed.
Hardware Impact: Two failed gated compiler passes exposed external dependency churn; the final successful pass was single-node/no-shared-compiler.

Problem: User issued another APEX rejection and required another full disk-backed proof cycle.
Solution: Re-read status/rationale, re-extracted prompt 1332, manually re-read core implementation files, reran production token scans, native field Roslyn audit, focused hot-method Roslyn audit, scoped diff check, source hash computation, and final gated build. Build succeeded: 0 warnings, 0 errors, 2.60 s.
Rejected Alternatives: Reuse the previous final JSON or run build while seven dotnet processes were active. Both would violate the explicit verification and host-resource rules.
Scalability potential: Runtime unchanged; proof artifacts match the same source hash `cb1eda0ab2de52bed59c16cd2ddabd715eb127a9b80863796d5d62894d4c4737`.
Hardware Impact: One short gated build after CPU dropped to 22.0%; no parallel compiler contention added.

Problem: User repeated the APEX rejection and required another fresh prompt/scanner/build cycle.
Solution: Re-read status/rationale, re-extracted prompt 1332 from disk, re-ran scoped Native field Roslyn audit, reran focused hot-path Roslyn audit, waited through an external `dotnet build .\Hecton8.slnx` instead of competing, then ran one Core build after CPU 10.3% and no compiler processes. Build succeeded: 0 warnings, 0 errors, 43.99 s.
Rejected Alternatives: Start a second compiler while external dotnet/csc was active, use the stale `703851...` hot scanner hash, or claim runtime profiler proof. All are false verification.
Scalability potential: Runtime unchanged. Accessibility remains VisualSync CBuffer shader interpolation driven by continuous `GlobalQualityWeight`, so low/middle/high/ultra differ visually without changing input truth or DTO layout.
Hardware Impact: One gated compiler pass after host load cleared. Static result remains 0 persistent native fields and 0 hot-path allocation hits; measured MX350/i3 runtime delta remains unavailable without Unity Profiler/GCMonitor.

Problem: Fresh online/API review exposed a concrete `GraphicsBuffer.LockBufferForWrite` contract violation: Unity requires `GraphicsBuffer.UsageFlags.LockBufferForWrite` at buffer construction.
Solution: Create both accessibility constant buffers with `GraphicsBuffer.Target.Constant` plus `GraphicsBuffer.UsageFlags.LockBufferForWrite`; fall back to global vector when `SystemInfo.supportsSetConstantBuffer` is false.
Rejected Alternatives: Keep the previous constructor and trust the editor path, or switch to `SetData` every dirty upload. The first throws on valid platforms; the second adds avoidable copy cost.
Scalability potential: Low devices use vector fallback when constant buffers are unsupported; Middle/High/Ultra keep the CBuffer route and can scale shader visual strength continuously with `GlobalQualityWeight`.
Hardware Impact: Prevents a cold accessibility/menu exception and keeps dirty VisualSync upload at one 16-byte write; measured i3/MX350 profiler proof is still absent.

Problem: The previous strict no-new-string loader made saved rebinds nonfunctional after restart when the override path was not already present in the InputSystem object graph.
Solution: Persist binding GUID hash in `controls.json`, validate the path bytes first, then cold-decode a bounded ASCII Unity control path only after all fail-closed checks pass.
Rejected Alternatives: Reject all unknown path bytes to satisfy a fake zero-GC claim. That breaks the user-visible input contract and silently discards legitimate bindings.
Scalability potential: Save identity is invariant across Low/Middle/High/Ultra. Quality weight never changes binding IDs, path schema, or action authority.
Hardware Impact: Hot frame cost remains 0 us. Cold load may allocate one managed string for a revived path; this is a deliberate compatibility cost, not a simulation-frame allocation.

Problem: `InputManager` still exposed Unity `SaveBindingOverridesAsJson`/`LoadBindingOverridesFromJson`, which reintroduced managed JSON strings behind the custom rebind route.
Solution: Keep the interface shape for compatibility but make those legacy methods inert; `RebindingManager` and UI use `ControlRemapper`.
Rejected Alternatives: Remove the methods and risk interface breakage, or keep Unity JSON as fallback. Removal is cross-domain churn; fallback violates the explicit `controls.json` contract.
Scalability potential: Runtime behavior is identical across device tiers. The saved cycles buy smoother pause/PDA menus rather than changing input truth.
Hardware Impact: Removes cold managed string serialization from the standard rebind route; exact menu-frame delta requires Unity Profiler.

Problem: `UserOptionsPersistence` inside Hecton8.Input still had delete-before-move, broad catch, and throw-on-capacity. It is not the `controls.json` target, but it is still domain code.
Solution: Add `TrySaveToDisk`, use `File.Replace`/`File.Move` without deleting the destination first, narrow catches, and convert payload overflow to a false return. Add a source-level test guard.
Rejected Alternatives: Blindly rewrite `options.h8cfg` away from `JsonUtility` without a migration test. That risks losing language/scalability settings and touching UI/localization behavior beyond the rebind task.
Scalability potential: Settings persistence remains cold and stable across Low/Middle/High/Ultra. Future migration should keep discrete scalability tier only as user preference, not gameplay truth.
Hardware Impact: Normal frame 0 us. Cold settings save is safer; `JsonUtility` residual still allocates and is explicitly tracked.

Problem: Final verification had to reflect the actual source after these fixes, not the previous green hash.
Solution: Re-ran scoped static scans and one gated build after CPU 48.6% and no `dotnet`/`csc`. Result: build succeeded, 0 warnings, 0 errors, 99.60 s. New combined touched-file hash is `c374226f5eefc5b0eb187ca3377eaaa2b1a763125c035bd1bd2694f7d442ef71`.
Rejected Alternatives: Reuse stale hash/build, or launch more Roslyn/dotnet tools after CPU later rose to 87.1%. Both would be false verification or host contention.
Scalability potential: Runtime unaffected; proof state now matches source.
Hardware Impact: One constrained compiler pass, no repeated build spam after host load rose.

Problem: `UserOptionsPersistence` still used Unity `JsonUtility` for cold `options.h8cfg`, leaving a managed JSON serializer in the Hecton8.Input domain.
Solution: Migrate the active settings payload to a bounded little-endian binary `H8OP` record stream inside the existing `H8CF` container. Preserve old JSON files through a cold strict parser that understands the previous `Records` array without calling `JsonUtility`.
Rejected Alternatives: Blindly delete legacy support, keep `JsonUtility` because it is cold, or replace it with Newtonsoft/System.Text.Json. Deleting legacy settings is user-hostile; Unity managed JSON keeps the violation; other JSON serializers are larger managed allocation surfaces.
Scalability potential: Low/Middle/High/Ultra share the same settings identity. Scalability tier remains a user preference record and does not alter gameplay truth, DTO layout, or authority routing.
Hardware Impact: Normal frame 0 us. Cold settings save avoids Unity JSON object/string reflection path and writes bounded bytes; exact i3/MX350 delta is unmeasured.

Problem: The same settings owner still used `new FileInfo` during portable read.
Solution: Use the already-open `FileStream.Length`, removing the managed `FileInfo` object from the load path.
Rejected Alternatives: Keep it because settings load is cold. The domain target is menu/settings hitch prevention, so avoidable managed objects are still debt.
Scalability potential: Runtime behavior and tiers unchanged.
Hardware Impact: Removes one cold managed object allocation per portable settings read.

Problem: Build verification after the binary options change could be false if I reused the earlier green build.
Solution: Waited for no `dotnet/csc` processes and CPU under 50%, then ran one constrained Core build. Build failed in non-domain `World/ScatterBackendBindingState.cs` on missing `_heightSamples`, `_cellStates`, and native memory sentinel symbols; no Hecton8.Input/UI errors appeared.
Rejected Alternatives: Edit World scatter code from input/UI ownership, start another build under CPU saturation, or claim green. All violate domain and reporting rules.
Scalability potential: My runtime changes are unaffected; external World compile wall prevents full workspace proof.
Hardware Impact: One constrained compiler pass; no repeated build spam.

Problem: The binary options reader could still mutate `_records` incrementally while parsing. A corrupt tail after valid records would leave partial settings state even though load returned false.
Solution: Stage binary and legacy records in `_writeRecords`, then call `ApplyStagedOptionRecords` only after the payload/array terminator is proven complete. Mark valid `H8CF` containers as portable immediately so corrupted portable files do not fall through into legacy text JSON interpretation.
Rejected Alternatives: Keep applying while parsing because settings are cold, or silently salvage partial settings. Partial salvage violates fail-closed persistence and makes settings state depend on file corruption position.
Scalability potential: Low/Middle/High/Ultra keep the same file identity and quality-tier semantics. A corrupt file now fails to default/cache behavior predictably instead of applying arbitrary partial records.
Hardware Impact: Adds one cold staging pass over at most 512 records. Normal frame cost remains 0 us; menu/settings load avoids inconsistent retries and undefined user option state.

Problem: `UserOptionsPersistence` public read accessors (`ScalabilityTier`, `HasKey`, `Get*` through `TryGetRecord`) could invoke `EnsureLoaded()`. That means a read route could do disk I/O, grow `_writeRecords`, mutate `_records`, and publish scalability from `ApplyLoadedScalabilityTier`.
Solution: Make read accessors pure over cached state: `ScalabilityTier` returns `_scalabilityTier`, `HasKey` and `TryGetRecord` require `_loaded`, and disk loading remains in `Awake` plus explicit write paths.
Rejected Alternatives: Rely on Unity `Awake` order and keep lazy reads. That hides a boot-order dependency and violates the project doctrine that read accessors must not mutate global state or allocate.
Scalability potential: Low devices avoid accidental menu-read disk hitches; Middle/High/Ultra behavior stays identical because settings truth is still loaded by the owner lifecycle, not by consumers.
Hardware Impact: Removes a possible cold read-triggered file I/O path from `SettingsManager.Load*`; hot frame remains 0 us.

Problem: Verification after this read-purity patch could not reuse the previous native-field report.
Solution: Re-ran the scoped native-field Roslyn audit over 9 copied files. Result: parse failures 0, native field declarations 0, persistent candidates 0, hash `d9841e268c8c81ed55869976dab50d01881ce67f259231bf7ad00aa56e633930`.
Rejected Alternatives: Use stale `ebcd...` report or whole-repo native audit that includes other agents' domains. Both would produce inaccurate 1332 evidence.
Scalability potential: Runtime unchanged; verification scope now matches current input/UI files plus the options owner and source test.
Hardware Impact: Static scanner only; no gameplay cost.

Problem: A post-patch build is required for full proof, but the host CPU gate is mandatory.
Solution: Checked compiler processes and CPU twice. No `dotnet/csc` processes were present, but CPU sampled at 100% then 99%, so no build was launched.
Rejected Alternatives: Run `dotnet build` under >50% CPU or claim the previous failed build covers the new patch. Both violate host-resource and evidence rules.
Scalability potential: Runtime unaffected; build proof remains pending until host load is legal.
Hardware Impact: Avoided adding compiler pressure to a saturated workstation.

Problem: After context compaction the build gate opened, but the Core build failed on a non-domain voxel edit.
Solution: Ran one constrained build only after CPU sampled at 38% and no `dotnet/csc` processes existed. The compiler failed at `Assets/_Project/Scripts/HectonVoxelEngine.cs(8434,38)` because `densityDecodeScale` is out of scope; `HectonVoxelEngine.cs` is already modified outside Agent 1332 ownership. Report the compile wall instead of mutating voxel code.
Rejected Alternatives: Touch voxel engine code from the input/UI agent, re-run repeated builds, or claim green on a workspace that does not compile. All are false verification.
Scalability potential: Input/UI/runtime behavior unchanged. Low/Middle/High/Ultra scalability remains governed by the existing input save identity and accessibility `GlobalQualityWeight` shader route.
Hardware Impact: One legal single-node compiler pass consumed 84.28 s and exposed external failure; no additional compiler spam launched.

Problem: Rebinding conflict confirmation displayed a conflict but did not actually make the binding set conflict-free; confirming only saved the new duplicate path.
Solution: Store the conflicting victim action and binding index during conflict detection, and on confirm apply an empty override path to the victim binding before saving. Unity documents empty `overridePath` as an effective binding disable route.
Rejected Alternatives: Keep "confirm anyway" semantics, force the player to cancel, or rewrite action assets destructively. "Confirm anyway" violates the no-conflict task; forced cancel is hostile; asset mutation is not runtime-safe.
Scalability potential: Low/Middle/High/Ultra share identical input truth. Quality weight does not touch binding identity or conflict policy.
Hardware Impact: Cold rebind UI path only. Expected gain is correctness, not frame time; normal frame remains 0 us.

Problem: `InputManager` still had bare `catch` blocks around binding display reads.
Solution: Replace them with narrow `InvalidOperationException` and `ArgumentOutOfRangeException` handling, keeping fail-closed returns.
Rejected Alternatives: Keep broad catches because the route is UI/cold. Broad catches hide dependency defects and violate the no-throw/no-catch audit pressure.
Scalability potential: Same behavior across device tiers; no quality-tier branch.
Hardware Impact: 0 us hot-path impact. Cold UI display failure becomes less opaque.

Problem: `InputManager` subscribed to `InputSystem.onDeviceChange` with method groups, creating delegate instances on lifecycle subscribe/unsubscribe cycles.
Solution: Cache `Action<InputDevice, InputDeviceChange>` once in `EnsureCachedDelegates` and use the cached field for add/remove.
Rejected Alternatives: Treat method group conversion as free or ignore it because it is lifecycle code. The project mandate targets menu/open hitches and avoidable managed churn.
Scalability potential: Low hardware avoids lifecycle heap churn; higher tiers can spend the saved budget on presentation, not input truth.
Hardware Impact: Small but real cold allocation removal on menu/activation cycles; exact bytes require Unity Profiler.

Problem: `INPUT_MIGRATION_GUIDE.md` still told future agents that rebinding persisted through PlayerPrefs and Unity JSON helpers.
Solution: Update the guide to the current bounded `controls.json` / `ControlRemapper` route and document conflict disabling through empty override path.
Rejected Alternatives: Leave stale docs because code was already fixed. Stale docs are a direct path to regression by the next agent.
Scalability potential: Runtime unaffected; process prevents future reintroduction of old managed persistence.
Hardware Impact: 0 runtime us.

Problem: PDA controls rebinding did not subscribe to `OnConflictDetected`, so a conflict raised from PDA controls could leave `RebindingManager` in pending-conflict state with no PDA-side confirmation route.
Solution: Add a cached PDA conflict delegate, subscribe/unsubscribe it with the other input-binding events, stage the conflict text in a preallocated modal char buffer, and show the existing modal confirm/cancel callbacks.
Rejected Alternatives: Let only `PauseControlsPanel` own conflict UI, or auto-confirm/auto-cancel inside `RebindingManager`. Pause-only handling fails when the PDA controls tab starts the rebind; automatic resolution would hide input authority from the player.
Scalability potential: Low/Middle/High/Ultra share the same input truth; visual cost exists only when the player enters a cold conflict flow.
Hardware Impact: Normal frame 0 us. One cached delegate and one 256-char buffer per PDA controls component; avoids a stuck service state rather than saving frame time.

Problem: Active rebinding could survive UI teardown or tab/section changes, leaving an Input System `RebindingOperation` or pending conflict callbacks after the visible owner disappeared.
Solution: Track `_ownsActiveRebind` per controls panel and cancel only that panel's own active rebind/pending conflict from disable/destroy, inactive input-consumption, PDA closed, and PDA tab-away events.
Rejected Alternatives: Global cancel from any inactive controls panel. Both Pause and PDA can reference the same binding service, so an inactive non-owner must not abort the other UI owner's rebind.
Scalability potential: All device tiers get deterministic fail-closed input behavior. Quality weight does not alter binding lifecycle or save identity.
Hardware Impact: Cold UI lifecycle only. Prevents disabled-action leaks and stale pending-conflict delegates; normal frame remains 0 us.

Problem: PDA controls refreshed all rows during `OnEnable`, `Start`, and GlobalRegistry hot-swap even when the controls tab was not visible.
Solution: Route those calls through `RefreshAllIfControlsTabActive()` and keep event-driven refresh for actual tab open/change.
Rejected Alternatives: Keep hidden refresh because it is menu-only. It still burns TMP/InputSystem work during service swaps and PDA lifecycle transitions.
Scalability potential: Low hardware avoids hidden menu work; higher tiers can spend saved UI budget on presentation without changing input truth.
Hardware Impact: Avoids O(row count) cold UI work on inactive tab entry/service swap. Exact i3/MX350 microseconds require Unity Profiler.

Problem: Build proof after the UI lifecycle patch could not be claimed from previous green passes.
Solution: Run one gated Core build only after CPU sampled at 24.37% and no compiler processes existed. It failed in non-domain Tools, SpatialAudio, Buoyancy, Gameplay, Inventory, World, and Odin attribute surfaces; no Agent 1332 files appeared in the compiler errors. After correcting owner-local cancellation, do not rerun while CPU is 52.20%, then 100% with eight dotnet processes and one VBCSCompiler.
Rejected Alternatives: Touch external compile walls, claim green, or launch another compiler above the explicit CPU gate. All violate domain ownership and evidence rules.
Scalability potential: Runtime design unchanged; verification is blocked by other owners.
Hardware Impact: One constrained compiler pass; no repeated build spam.

Problem: Verification had to reflect the new source, not the earlier external-voxel build wall.
Solution: Wait for the build gate, run scoped `VaultNativeAliasRoslynAudit` over 9 copied files, then run one constrained Core build. Audit result: 0 parse failures, 0 native fields, 0 persistent native candidates. Build result: 0 warnings, 0 errors, 70.19 s.
Rejected Alternatives: Reuse stale failed-build report, run under CPU saturation, or run repeated compilers while sibling agents work.
Scalability potential: Runtime unaffected; proof now matches the current source hash `3c91bf78b4745433c9950a21d4279c5ebe971a42829913db9f44dadfe86ac6ca`.
Hardware Impact: One gated single-node compiler pass; no build spam.

Problem: The conflict-closure patch disabled victim bindings with `ApplyBindingOverride(bindingIndex, string.Empty)`, but `ControlRemapper` still treated empty `overridePath` as absent and skipped it during save. That meant a conflict victim could resurrect after restart.
Solution: Preserve the `null` vs empty-string distinction. Save skips only `overridePath == null`; `"path":""` is accepted by the parser; zero `PathByteLength` validates as the disabled-state record and applies `string.Empty` directly without cold path decoding.
Rejected Alternatives: Re-enable old Unity JSON helpers, serialize a sentinel string such as `<Disabled>`, or delete the original binding. Unity documents empty override path as the runtime disable route; sentinels and asset mutation would create a private non-Unity contract.
Scalability potential: Low/Middle/High/Ultra share the same binding identity and conflict policy. Quality weight never mutates input truth.
Hardware Impact: Cold rebind save/load only. Disabled-path load avoids the bounded `new string` decode path entirely; hot frame remains 0 us.

Problem: Input display helpers used the default binding path when `effectivePath` was empty and `overridePath` was empty. That made UI labels lie by showing the old key for an explicitly disabled binding.
Solution: Add `TryGetActiveBindingPath` and route preferred path, display text, char-buffer display, binding suitability, and glyph markup through it. If `overridePath != null` and `effectivePath` is empty, the binding is treated as disabled and display returns false.
Rejected Alternatives: Show a placeholder string from this layer or keep fallback for convenience. Placeholder policy belongs to UI callers; this owner must report the binding truth.
Scalability potential: Same behavior across device tiers and input devices; no quality-tier branch.
Hardware Impact: No new allocation; only one extra null check on cold UI binding display reads.

Problem: DTO layout proof was aligned, but `InputBindingTelemetryEntry` and `ControlRemapIoResult` used wide private padding fields. The local ARM64 law asks for explicit byte-padding variables.
Solution: Replace wide private padding with byte pads at the same offsets while preserving public fields, struct sizes, and byte maps.
Rejected Alternatives: Leave wide padding because CLR layout was already correct. The project gate is source-audited, so the layout proof must be unambiguous.
Scalability potential: DTO ABI stays stable across cheap and high-end devices.
Hardware Impact: No runtime cost. The change is layout-proof hygiene, not a performance feature.

Problem: The new disabled override and padding fixes required fresh proof.
Solution: Ran production source scan, scoped `VaultNativeAliasRoslynAudit`, `git diff --check`, and one gated Core build. Results: broad catch 0, throw new 0, PlayerPrefs 0, legacy fallback 0, empty override skip 0, AUP casts 0, wide padding 0; native fields 0/persistent 0; build succeeded 0 warnings/0 errors in 83.18 s. Report artifact: `Docs/Reports/INPUT_DISABLED_OVERRIDE_REAUDIT_1332.json`.
Rejected Alternatives: Reuse the prior green build or run the compiler while CPU was 100%. Both would be false evidence or host contention.
Scalability potential: Runtime unchanged; proof now matches source hash `6b90b39741919f440f58d14856a256360824e5cf266b2c371283923e0be9ae82`.
Hardware Impact: One legal single-node compiler pass after CPU dropped to 35.9%; no repeated build spam.

Problem: `ControlRemapper` still let invalid filesystem path forms escape through cold `controls.json` I/O because only unauthorized/IO exceptions were caught.
Solution: Add narrow `ArgumentException` and `NotSupportedException` fail-closed branches to save, load, atomic replace, temp cleanup, and telemetry dump. Route save/load catches through `MarkIoFailure` to keep telemetry numeric and bounded.
Rejected Alternatives: `catch (Exception)`, pre-validating with platform-specific path character lists, or leaving cold invalid-path throws to Unity. Broad catch hides defects; char-list validation is incomplete across platforms; uncaught cold settings exceptions violate fail-closed persistence.
Scalability potential: Low/Middle/High/Ultra behavior is identical. Invalid controls paths now fail closed without mutating input truth or save identity.
Hardware Impact: Normal frame 0 us. Cold invalid-path failure avoids exception escape into menu/settings caller; exact profiler delta not measured.

Problem: A duplicate `UserOptionsPersistence` instance could be destroyed after loading from disk and then run `OnDestroy -> OnServiceShutdown -> Save`, creating a cold write from a non-owner.
Solution: Add `DestroyDuplicateInstance`, setting `_serviceShuttingDown` and `_serviceShutdownComplete` before `Destroy(gameObject)` in both duplicate checks.
Rejected Alternatives: Let `OnDestroy` decide based on `_serviceRegistered` only or remove duplicate checks. `_serviceRegistered` false still allowed `Save`; removing duplicate checks breaks single-owner doctrine.
Scalability potential: Low devices avoid a duplicate cold settings write; higher tiers keep the same user option identity and no gameplay truth change.
Hardware Impact: Saves one accidental cold disk write in duplicate-service scenes; hot frame remains 0 us.

Problem: Build proof after the latest patch could not be honestly marked green.
Solution: Ran one constrained build only when CPU averaged 42.8% and no `dotnet/csc` existed. It failed in non-domain Fauna/Gameplay/Visor files; after that CPU stayed 91.9-100%, so no second build was launched.
Rejected Alternatives: Edit Fauna/Gameplay/Visor from the input/UI agent, spam builds under CPU saturation, or claim green. All violate domain and evidence rules.
Scalability potential: Runtime behavior of input/UI changes remains tier-invariant; verification is externally blocked.
Hardware Impact: One legal single-node compiler pass; no repeated compiler pressure under saturated CPU.

Problem: `UserOptionsPersistence.Awake` still loaded `options.h8cfg` before checking whether another runtime owner was already registered.
Solution: Move the duplicate-owner check before `LoadFromDisk`, so duplicate instances self-destruct without disk I/O.
Rejected Alternatives: Keep the read because it is cold, or rely only on shutdown suppression. Cold duplicate I/O still violates owner-before-work and can hitch scene boot on weak storage.
Scalability potential: Low hardware avoids duplicate settings reads; Middle/High/Ultra keep identical settings truth.
Hardware Impact: Removes one accidental cold options file read from duplicate-service scenes; hot frame remains 0 us.

Problem: `RebindingManager` bootstrapped telemetry and assigned `ActiveRuntimeInstance` before proving it was the registered input-binding owner.
Solution: Add play-mode and duplicate-service gates before telemetry bootstrap; assign `ActiveRuntimeInstance` and load initial overrides only after `_registeredService` is true.
Rejected Alternatives: Let duplicates briefly touch `GlobalDataVault` or rely on later unregister. That violates one-owner doctrine and can allocate native buffers from a losing owner.
Scalability potential: Input truth stays tier-invariant. Low devices avoid duplicate cold Vault/bootstrap work; higher tiers keep the same deterministic service route.
Hardware Impact: Avoids duplicate cold telemetry bootstrap and initial override load; hot frame remains 0 us.

Problem: Reset/delete cleanup for `controls.json` and `controls.json.tmp` used repeated delete code and only caught unauthorized/IO exceptions.
Solution: Replace it with `TryDeleteOverridesFile`, covering `ArgumentException` and `NotSupportedException` as narrow fail-closed cases. Verified against Microsoft Learn: `File.Exists` returns false on invalid paths, while `File.Delete` can throw invalid-format exceptions.
Rejected Alternatives: Broad catch or leaving temp cleanup partially hardened. Broad catch hides defects; partial cleanup leaves stale temp files after reset.
Scalability potential: Low/Middle/High/Ultra behavior is identical; reset semantics do not affect gameplay truth or DTO layout.
Hardware Impact: Cold reset path only. Normal frame cost 0 us.

Problem: The host did not allow a legal build after this final owner-order patch.
Solution: Sampled CPU/compiler state repeatedly and did not launch the compiler while CPU was 100/100/96.2/65.8/96.6/100/98.4/100/98/100/91.1/97.1 with no dotnet/csc.
Rejected Alternatives: Violate the >50% CPU build ban or claim the previous external-failure build covers new edits.
Scalability potential: Runtime unaffected; proof remains static until a legal build slot opens.
Hardware Impact: Avoided adding compiler pressure to a saturated workstation.

Problem: `ControlRemapper.TryLoadOverrides` cleared current overrides before applying parsed records, but a rare Unity `ApplyBindingOverride` failure after prevalidation could leave a partially applied control map.
Solution: Add `TryApplyOverridePath` with narrow Unity argument/state catches and clear all overrides if `applied != recordCount`, returning numeric `UnsupportedPath` telemetry.
Rejected Alternatives: Broad `catch (Exception)`, trusting prevalidation as a transaction, or trying to restore an unknown previous override set through managed JSON. Broad catch hides defects; prevalidation is not a write transaction; managed restoration reintroduces the old persistence path.
Scalability potential: Low/Middle/High/Ultra keep identical input truth. A failed controls.json load now collapses to no overrides instead of a half-mutated map.
Hardware Impact: Cold load only. Normal frame cost 0 us; failure path avoids user-facing inconsistent controls.

Problem: Verification had to be rescoped because previous `rg` attempts produced all-repo noise from unrelated dirty domains.
Solution: Use exact-file `Select-String` scans over the 8 production files plus a scoped SHA-256 over 9 files. Result: forbidden hot-path token hits 0; native collection field declarations 0; hash `2169ef4aa3582c3bedcf70facf963337d43a35070a44a46eaaf058a455323dbf`.
Rejected Alternatives: Report noisy scanner output as proof or scan third-party/tools as if they were Agent 1332 domain files.
Scalability potential: Runtime unaffected; proof now maps to the real ownership boundary.
Hardware Impact: Static verification only.

Problem: The host still did not allow a legal build after the fail-closed apply patch.
Solution: Sampled compiler process state and CPU; no dotnet/csc existed, but CPU was 100, 97.6901577906802, 90.6931743591841, 63.4589835825046, 55.6879675323122, 25.8606143932384, 97.5005539700729, and 72.7217684570501. The one sub-50 sample did not hold on the next pair, so build was not launched.
Rejected Alternatives: Violate the explicit build CPU gate.
Scalability potential: Runtime unaffected; build proof remains pending.
Hardware Impact: Avoided compiler pressure on a saturated workstation.

Problem: `IInputBindingService.ClearOverrides` still exposed the stale parameter name `clearPlayerPrefs` even though the implementation and persistence route now use `controls.json`.
Solution: Rename the interface parameter to `clearSavedOverrides` and add an editor source guard. This keeps the public contract aligned with the actual save owner and prevents future named-argument calls from resurrecting a PlayerPrefs mental model.
Rejected Alternatives: Leave the name because calls are currently positional, or add an overload. Positional-only safety is fragile; an overload would add API surface for no behavior change.
Scalability potential: Low/Middle/High/Ultra behavior is identical. Input truth and save identity remain independent of quality tier.
Hardware Impact: 0 us runtime. This is a contract correctness fix.

Problem: `InputManager` preallocated only 8 display-style cache entries for devices, which can resize on platforms with keyboard, mouse, multiple gamepads, XR controllers, and virtual devices.
Solution: Increase the initial Dictionary capacity to 32 and guard the capacity in the editor test. This keeps the existing cold managed dictionary route but reduces avoidable device-change allocations without changing semantics.
Rejected Alternatives: Replace the dictionary with a fixed array now, or ignore the issue because device changes are cold. A fixed-array rewrite is wider and riskier inside the current shared file; ignoring a cheap capacity fix leaves known cold allocation pressure.
Scalability potential: Low devices avoid a resize during device discovery/hotplug; higher tiers keep identical display-style truth while supporting more attached devices.
Hardware Impact: Hot frame 0 us. Cold device-change path avoids at least one Dictionary resize when device count exceeds 8.

Problem: Verification after the stale-contract patch could not legally run build or the dotnet Roslyn audit.
Solution: Ran exact-file static scans and `git diff --check`, computed scoped hash `d324aca10534147c949bda3ff0d62e8d270bdf15c32b15970bac6d701144fd88`, and blocked build/Roslyn audit because CPU averaged 89.41, 100, 100, 99.52, 91.08, 84.37, 70.76, and 62.41 with no dotnet/csc.
Rejected Alternatives: Launch dotnet above the explicit 50% CPU gate or falsely mark full verification green.
Scalability potential: Runtime unaffected; verification remains static until the host opens a legal build slot.
Hardware Impact: Avoided adding compiler/runtime pressure to a saturated workstation.

Problem: Shared rebind service events could make hidden Pause/PDA controls panels refresh all binding rows before checking whether the panel/tab was active.
Solution: Move active-state guards before `RefreshAllBindingsNow()` / `RefreshAllBindings()` in rebind-complete and rebind-cancel handlers for both assigned UI panels, and add source guards for the ordering.
Rejected Alternatives: Keep the refresh because handlers are event-driven, or unsubscribe PDA when the tab is inactive. Keeping the refresh wastes UI work from unrelated panels; tab-level unsubscribe would be a wider lifecycle rewrite with more missed-event risk.
Scalability potential: Low hardware avoids hidden O(row count) TMP/UI refresh work; Middle/High/Ultra retain identical binding truth and visual state when the panel is visible.
Hardware Impact: Hot simulation 0 us. Inactive-panel rebind completion/cancel now costs a branch instead of refreshing every configured row.

Problem: Verification after the UI event guard patch could not legally run build or the dotnet Roslyn audit.
Solution: Ran exact-file static scans and `git diff --check`, computed scoped hash `d348fa025287cbab56ede4d673b1c4f6467f82fdaf30c9094c540c6f7571cbee`, and blocked build/Roslyn audit because CPU averaged 99.13, 81.05, 94.6, 100, 98.07, 81.01, 100, and 65.52 with no dotnet/csc.
Rejected Alternatives: Launch dotnet above the explicit 50% CPU gate or claim a compile proof not produced in this pass.
Scalability potential: Runtime unaffected; proof remains static until the host opens a legal build slot.
Hardware Impact: Avoided compiler/runtime pressure on a saturated workstation.

Problem: `RebindingManager` could be disabled or destroyed while an interactive rebind or pending conflict callback was still alive.
Solution: Route `OnDisable` and `OnDestroy` through `CancelRebindOrPendingConflict()` before unregistering the service/runtime instance. The helper closes the active operation or pending conflict and clears owner-local context.
Rejected Alternatives: Dispose only `_activeRebind`, rely on Unity object destruction to invalidate callbacks, or leave pending conflict UI to caller cleanup. Disposal-only misses pending conflict; destruction does not erase managed delegates immediately; caller cleanup is not an owner guarantee.
Scalability potential: Low/Middle/High/Ultra behavior is identical. Input truth and conflict policy remain independent of `GlobalQualityWeight`.
Hardware Impact: Hot frame 0 us. Cold lifecycle path avoids stuck disabled actions and stale conflict callbacks that would force user recovery.

Problem: `ClearOverrides()` still canceled only active operations, not pending conflict resolution.
Solution: Make `CancelRebind()` handle pending conflict when no active operation exists; make `ClearOverrides()` call the lifecycle helper; include pending conflict in `IsRebinding` so a second rebind cannot start while conflict resolution is unresolved.
Rejected Alternatives: Add a separate public `CancelConflict` route, silently overwrite pending conflict context on next rebind, or leave clear/reset to race the old callbacks. Extra API surface is unnecessary; overwriting loses cancellation semantics; racing callbacks can reapply or save stale override state.
Scalability potential: Same input contract across weak devices and high-end machines; no tier-specific behavior.
Hardware Impact: Hot frame 0 us. Cold reset/rebind path avoids inconsistent controls and duplicated UI work.

Problem: Verification after the lifecycle patch could not legally run build or the dotnet Roslyn audit.
Solution: Ran exact-file static scans, native token classification, `git diff --check`, hot-loop marker scan, AUP cast scan, and scoped SHA-256. Build/Roslyn remained blocked because CPU sampled 74.6, 82.28, then four 100% windows with no dotnet/csc.
Rejected Alternatives: Launch dotnet above the explicit 50% CPU gate or report stale compile proof.
Scalability potential: Runtime unaffected; proof remains static until a legal build slot opens.
Hardware Impact: Avoided adding compiler pressure to a saturated workstation.

Problem: `IInputBindingService.SaveOverrides`, `LoadOverrides`, and `ClearOverrides` returned `void`, so UI callers could not distinguish a successful controls.json operation from fail-closed refusal, missing input runtime, malformed load, or filesystem delete failure.
Solution: Convert the persistence contract to `bool`, make `RebindingManager` return false on every failed persistence/deletion/runtime route, and update Pause/PDA UI to show success only after a true result.
Rejected Alternatives: Keep `void` and infer success from event callbacks, or add panel-local guesses. Events do not report failures and panel-local guesses would duplicate persistence rules outside the owner.
Scalability potential: Low/Middle/High/Ultra behavior is identical. Input truth and saved identity remain independent of quality weight.
Hardware Impact: Cold UI/settings branch only; hot simulation 0 us. Avoids user-driven repeated I/O attempts after false success on weak storage.

Problem: Missing `controls.json` represented default bindings, but `LoadOverrides()` returned no-op, leaving unsaved in-memory overrides alive while the UI said bindings were reverted.
Solution: Route missing-file load through a deterministic default-state apply: clear runtime binding overrides, raise the load event for UI refresh, and return true. Malformed files still return false without mutating current bindings.
Rejected Alternatives: Treat missing file as failure, or clear on every failed load. Missing file is a valid default state; clearing after malformed/corrupt data would violate fail-closed parser guarantees.
Scalability potential: Same across cheap and high-end devices; no quality-tier branch.
Hardware Impact: Cold cancel/load only. Hot frame 0 us.

Problem: Some early `ControlRemapper` fail-closed exits populated `ResultCode`/`FaultFlags` but did not build the fixed telemetry entry, weakening numeric failure proof.
Solution: Add `MarkFailure` to fill `ControlRemapIoResult.Telemetry` on early invalid/buffer-overflow save/load exits.
Rejected Alternatives: Rely on the caller to synthesize telemetry, or leave default telemetry for rare branches. Caller synthesis would duplicate operation-specific byte/count fields; default telemetry loses the numeric failure code.
Scalability potential: Low devices get deterministic failure evidence without extra normal-path cost; higher tiers keep the same ring contract.
Hardware Impact: Cold error path only. Normal save/load success path unchanged; hot frame 0 us.

Problem: The input migration guide did not document the new bool persistence result contract or the missing-file default semantics.
Solution: Update only `Assets/_Project/Scripts/Input/INPUT_MIGRATION_GUIDE.md` with two concise bullets.
Rejected Alternatives: Leave docs stale, or inflate global architecture documents. Stale docs cause regression; broad doc churn wastes review bandwidth and touches other agents' ownership.
Scalability potential: Runtime unaffected.
Hardware Impact: 0 runtime us.

Problem: Build verification after this pass was required but illegal under the host-resource gate.
Solution: Sampled compiler process state and CPU repeatedly. No `dotnet/csc` existed, but CPU averages were 78.49, 99.94, 99.81, and 100.0, so no build or dotnet Roslyn audit was launched.
Rejected Alternatives: Violate the explicit >50% CPU build ban or claim a compile proof from stale source.
Scalability potential: Runtime unaffected; verification remains static until the workstation opens a legal compiler window.
Hardware Impact: Avoided adding compiler pressure to a saturated host.

Problem: `ClearOverrides` cleared runtime binding overrides before deleting `controls.json`; if the delete failed, the current session was reset while disk still contained stale overrides.
Solution: Move persisted delete before runtime clear. A reset-all now mutates runtime only after saved state is actually removed.
Rejected Alternatives: Keep runtime-first and rely on the false return. That still creates a split-brain state between session and next boot.
Scalability potential: Same behavior across low/middle/high/ultra devices; input truth is not quality-weighted.
Hardware Impact: Cold reset path only; hot frame 0 us.

Problem: Per-row reset removed an override before saving; if `saveAfterRowReset` failed, runtime state changed while `controls.json` did not.
Solution: Snapshot the previous `overridePath` and restore it with a narrow fail-closed helper when save fails in Pause and PDA controls panels.
Rejected Alternatives: Leave the runtime mutation and show a failure status only. Status alone does not restore deterministic input truth.
Scalability potential: Low devices avoid inconsistent input after storage failure; high-end behavior is identical.
Hardware Impact: Cold row-reset failure path only. Normal frame 0 us.

Problem: Pause settings-close autosave still ignored the `SaveOverrides()` result after the contract became result-bearing.
Solution: Consume the bool and route failure to existing static status text.
Rejected Alternatives: Ignore the result because the panel is closing. That leaves an unchecked persistence route in the domain.
Scalability potential: Runtime unaffected; no tier branch.
Hardware Impact: Cold settings close only; hot frame 0 us.

Problem: Verification after the reset transaction patch was required but illegal under the host-resource gate.
Solution: Ran exact-file static scans, native token classification, hot-loop/AUP scans, diff check, and hash computation. Build/Roslyn remained blocked because CPU samples were 98.84, 100, 100, and 92.85 with no dotnet/csc.
Rejected Alternatives: Launch dotnet above the explicit 50% CPU gate or report stale build proof.
Scalability potential: Runtime unaffected; verification remains static until a legal compiler window opens.
Hardware Impact: Avoided adding compiler pressure to a saturated host.

Problem: A successful interactive rebind could fail its automatic `controls.json` save and still emit completion events, making UI and runtime claim success while disk persistence remained stale.
Solution: Snapshot the previous override path for active and conflict-victim bindings. If automatic save fails after a normal rebind or conflict confirmation, restore the prior runtime overrides, raise `OnRebindCanceled`, and emit `OnOverridesSaveFailed` for subscribed UI panels.
Rejected Alternatives: Keep completion and show a warning, or try to save again in UI. Completion after failed persistence is a false state; UI retries duplicate persistence ownership.
Scalability potential: Low/Middle/High/Ultra behavior is identical. Binding truth and save identity remain independent of quality tier.
Hardware Impact: Cold rebind completion only. Hot frame 0 us; weak storage now fails closed instead of creating a next-boot mismatch.

Problem: Conflict cancel used default removal semantics, losing an older user override when the new conflicting override was canceled.
Solution: Carry `_pendingConflictPreviousOverridePath` and restore that exact path in `CancelRebindAfterConflict`.
Rejected Alternatives: `RemoveBindingOverride` on cancel. It only restores default, not the previous user-selected binding.
Scalability potential: Same input truth on weak and high-end devices; no quality-weight branch.
Hardware Impact: Cold conflict UI path only; hot frame 0 us.

Problem: Per-row reset rollback still leaked when `saveAfterRowReset` was true but no `IInputBindingService` owner was available.
Solution: Treat missing rebind service as persistence failure in Pause/PDA row reset, restore the prior override, refresh the row, and show the existing save-failed status.
Rejected Alternatives: Let runtime reset proceed because the owner is missing. That creates session/disk split-brain and violates one-owner persistence.
Scalability potential: Low devices avoid inconsistent controls after service-order failures; higher tiers keep identical semantics.
Hardware Impact: Cold row-reset path only. Normal frame 0 us.

Problem: `UserOptionsPersistence.OptionsPath` was a read accessor that lazily wrote `_optionsPath` and related path fields.
Solution: Make `OptionsPath` a pure cached read and call `EnsureOptionsStoragePaths()` from explicit owner phases: Awake, save, and load.
Rejected Alternatives: Keep lazy mutation because it is convenient. Global doctrine requires read accessors to be pure and not mutate owner state.
Scalability potential: Runtime semantics unchanged across all quality tiers.
Hardware Impact: 0 hot us. Cold path resolution now happens in explicit lifecycle/I/O phases only.

Problem: Verification after autosave/row-reset closure could not legally run build or dotnet Roslyn audit.
Solution: Ran exact-file static scans, native token classification, AUP scan, diff check, prompt extraction, and scoped hash `a10e440283d6642bca00e57c0968fcabf6ad97885052bb308d199d42d7c1e30e`. Build/Roslyn remained blocked because CPU sampled 57.91, 80.70, and 99.82 with no dotnet/csc.
Rejected Alternatives: Launch dotnet above the explicit 50% CPU gate or claim stale build proof.
Scalability potential: Runtime unaffected; proof remains static until a legal compiler window opens.
Hardware Impact: Avoided adding compiler pressure to a saturated host.

Problem: Pause/PDA controls refresh still resolved `INativeInputManagerRuntime` inside each row refresh; PDA also resolved it inside `ResolveBindingIndex`, multiplying GlobalRegistry fallback/read-accessor calls during visible controls refresh.
Solution: Hoist input runtime resolution once per `RefreshAllBindings` pass and pass the cached service into `RefreshRowBinding`, `UpdateStatusForSelected`, and PDA `ResolveBindingIndex`. Keep wrapper methods for single-row event paths only where there is no existing cached service.
Rejected Alternatives: Cache a long-lived UI field for the input runtime or leave per-row lookups because the menu is cold. A long-lived field risks stale hot-swap state unless fully lifecycle-owned; per-row lookup violates the cold-DI intent and wastes UI-open budget.
Scalability potential: Low hardware avoids repeated registry fallback during controls menu refresh; Middle/High/Ultra keep identical input truth and can spend UI budget on visual polish instead of redundant dependency discovery.
Hardware Impact: Hot simulation frame 0 us. Cold visible controls refresh saves O(row count) service lookups; exact Unity profiler timing not measured.

Problem: Build verification after the UI refresh hoist was legal under CPU/process gates but failed because generated Unity project files are absent from the workspace root.
Solution: Attempted one constrained build only after CPU 20/41% and no dotnet/csc processes. `Hecton8.Core.csproj` was absent; `dotnet build .\Hecton8.slnx` failed with MSB3202 for missing generated `.csproj` files. Roslyn native alias audit was run against a copied exact 9-file scope and passed with parseFailures 0 and persistent native fields 0.
Rejected Alternatives: Generate Unity `.csproj` files from this agent, edit `.slnx`, or claim a build proof. Project-file generation is workspace/integration ownership, not the input/UI domain.
Scalability potential: Runtime unaffected; verification remains static/Roslyn until Unity regenerates project files or the integrator restores them.
Hardware Impact: The failed build exited in under one second; no compiler contention was left running.

Problem: `StartInteractiveRebind` assumed `InputAction.PerformInteractiveRebinding` always returned a valid operation before registering callbacks and starting the operation.
Solution: Add an explicit null-operation guard immediately after `PerformInteractiveRebinding`; route it through the same fail-closed cleanup as Unity/InputSystem exceptions.
Rejected Alternatives: Trust the current package behavior or catch broad `Exception`. Package edge behavior is not a contract, and broad catch hides real dependency defects.
Scalability potential: Low/Middle/High/Ultra input truth is identical. This is a cold rebind setup guard, not quality-scaled behavior.
Hardware Impact: Hot frame 0 us. Cold rebind-start adds one null branch and prevents a menu-path exception cascade on weak devices.

Problem: `TryReadPortableOptionsFile` read the scalability tier header before validating/applying the payload, so a corrupt `options.h8cfg` payload could still apply a tier from the same broken file.
Solution: Stage the header tier in locals, validate/apply payload first, and only then publish `scalabilityTier`/`hasScalabilityTier` to the caller.
Rejected Alternatives: Treat the header as independently authoritative or delete the file. Header-only acceptance is partial state application; deletion is destructive and outside this owner path.
Scalability potential: Low devices fail closed to current/default tier instead of silently accepting a corrupt file tier. Higher tiers keep identical persistence semantics.
Hardware Impact: Cold settings load only. Normal frame 0 us; corrupt-file path avoids inconsistent quality profile state.

Problem: Verification after the latest fail-closed patch needed a compile attempt, but the host remained above the mandated CPU limit.
Solution: Ran exact-file source scans, native token/field scans, AUP scan, `git diff --check`, and scoped hash. Deferred `dotnet build` because no compiler process existed but CPU sampled 59%, 100%, 64%, and 100%.
Rejected Alternatives: Launch `dotnet` above the explicit 50% CPU ceiling or reuse stale build proof.
Scalability potential: Runtime unaffected; proof remains static until a legal compiler window opens.
Hardware Impact: Avoided adding compiler pressure to a saturated workstation.

Problem: `ControlRemapper.TryDumpTelemetry` resolved the Vault telemetry ring and wrote `Dump_1332.bin` directly from the returned `NativeArray` raw pointer. Filesystem I/O can stall outside a compaction-safe execution phase, so the view could outlive the lock/pin guarantee expected by the GlobalDataVault rules.
Solution: Copy the fixed 300-entry ring into an `Allocator.Temp` byte snapshot while holding `TryAcquireWriteLock`, release the write lock in `finally`, then perform directory creation and file writing from the snapshot. Add an editor source guard for lock-copy-release-before-I/O ordering.
Rejected Alternatives: Keep `TryResolveHandle` for dump because the path is fault-only, or hold the write lock during `FileStream.Write`. The first risks a dangling view during compaction; the second blocks compaction behind disk latency.
Scalability potential: Low devices keep normal-frame cost at 0 us and pay a bounded 19.2 KB copy only on fault dump. Middle/High/Ultra can inspect the same binary ring without changing DTO layout or save identity.
Hardware Impact: Normal frame 0 us. Fault path adds one native memcpy before I/O; expected below 10 us for 19.2 KB on i3/MX350, storage latency remains outside frame-critical flow.

Problem: Verification after the telemetry snapshot patch needed compiler proof, but the host CPU gate remained closed.
Solution: Ran exact-file production scans, native token/field scans, UI delegate scan, AUP scan, `git diff --check`, and scoped hash `5733a2b6adf30f5407897f0a80afe5bfce625f95f4a597e98d93f9afb44668f3`. Deferred `dotnet`/Roslyn because no compiler process existed but CPU sampled 99%.
Rejected Alternatives: Launch `dotnet` above the explicit 50% CPU ceiling or claim the previous build/hash covered the new code.
Scalability potential: Runtime unaffected; proof remains static until a legal compiler window opens.
Hardware Impact: Avoided adding compiler pressure to a saturated workstation.

Problem: `TryApplyBinaryOptionsPayload` accepted a fully valid prefix without requiring the cursor to consume the exact declared payload length.
Solution: Add an `index != payloadLength` fail-closed check before `ApplyStagedOptionRecords`.
Rejected Alternatives: Ignore trailing bytes because the known records were valid. That lets a corrupt `options.h8cfg` partially apply state.
Scalability potential: Low/Middle/High/Ultra behavior is identical. Settings truth is not quality-scaled.
Hardware Impact: Cold settings load only. Hot simulation frame remains 0 us; corrupt-file path exits before dictionary mutation.

Problem: Legacy options JSON migration skipped malformed record objects and applied the remaining records, creating partial state from corrupt data.
Solution: Change malformed `TryReadLegacyOptionRecord` to return false for the whole migration and keep all writes staged until validation succeeds.
Rejected Alternatives: Preserve best-effort migration. Best-effort persistence is dangerous for input/settings contracts because it silently mixes old and new user state.
Scalability potential: Low devices fail closed to current/default settings instead of accepting partial corrupt state; high-end devices keep the same behavior.
Hardware Impact: Cold legacy migration only. One branch per record; hot frame 0 us.

Problem: A first attempt to force legacy JSON to start with `Records` would have broken real old `JsonUtility` files where `Version` appears before `Records`.
Solution: Implement a top-level property-range parser that validates the root object, allows old `Version`, rejects duplicate `Records`, rejects root-tail garbage, and parses only the validated `Records` array range.
Rejected Alternatives: Keep unscoped `TryFindJsonProperty`, or require `Records` as the first property. Unscoped search can accept nested/garbage shapes; first-property strictness breaks known legacy format from `HEAD`.
Scalability potential: Runtime quality tiers unaffected. The parser is cold migration only and does not change DTO layout or gameplay authority.
Hardware Impact: Adds a bounded cold scan of one settings file. i3/MX350 impact is below menu-frame relevance; exact profiler timing not measured.

Problem: Verification after the options strictness patch needed compiler proof, but the host gate was closed by active compiler processes and high CPU.
Solution: Ran exact-file static scans, native token/field scan, UI delegate scan, AUP scan, diff check, and scoped SHA-256 `27351f49236fe1c42585241be9abbe5b6377b3a731d93ab3f94a67607413a683`. Deferred build/Roslyn dotnet audit because two dotnet processes were active and CPU sampled at 80%.
Rejected Alternatives: Launch another `dotnet` under active compiler load or falsely reuse stale build proof.
Scalability potential: Runtime unaffected; verification remains static until a legal compiler window opens.
Hardware Impact: Avoided adding compiler pressure to a busy workstation.

Problem: `TryReadPortableOptionsFile` truncated reads to `FixedOptionsFileBytes`, so a file with appended garbage beyond the fixed H8CF container could still be accepted.
Solution: Reject `fileLength > FixedOptionsFileBytes` before reading the header.
Rejected Alternatives: Ignore bytes after the fixed container. That hides corruption and weakens deterministic persistence.
Scalability potential: Runtime quality tiers unaffected. Settings identity remains the same across low/middle/high/ultra devices.
Hardware Impact: Cold settings load only; one length comparison. Hot frame 0 us.

Problem: Future H8CF versions greater than the current `FileVersion` were parsed by the current binary reader, assuming compatibility that does not exist as a contract.
Solution: Reject `version <= 0` and `version > FileVersion` before selecting binary or legacy payload parsing.
Rejected Alternatives: Treat unknown future versions as current binary payloads. That is a forward-compatibility lie and can apply corrupt settings.
Scalability potential: Same fail-closed behavior on weak and high-end devices; no quality branch.
Hardware Impact: Cold settings load only; one integer range check.

Problem: Legacy JSON record parsing used unscoped property search, so nested/spoofed `Key`/`Type` fields or malformed value suffixes could be accepted.
Solution: Parse legacy record properties through validated top-level ranges, bound record object end to the validated `Records` array, and require scalar value tokens to be fully consumed.
Rejected Alternatives: Keep the old best-effort parser because migration is cold. Cold persistence still owns user settings truth and must fail closed.
Scalability potential: Low devices avoid broken settings after corrupt migration; Middle/High/Ultra keep the same saved identity.
Hardware Impact: Cold legacy migration only. Extra scans are bounded by the 64 KB options payload cap; hot frame 0 us.

Problem: Optional legacy fields needed to distinguish absent from invalid/duplicate.
Solution: Extend the top-level property finder with `out bool found`; optional fields now return true only for well-formed-not-found, and return false for malformed/duplicate state.
Rejected Alternatives: Treat any failed optional lookup as absent. That lets corrupt records bypass validation.
Scalability potential: Runtime unaffected; corruption handling is deterministic across hardware tiers.
Hardware Impact: Cold legacy migration only.

Problem: Verification after legacy range hardening needed compile proof, but the CPU gate was closed.
Solution: Ran exact-file static scans, native token/field scan, UI delegate scan, AUP scan, diff check, and scoped SHA-256 `cac77ee7b442e153e6d7c717e911af60f1a2be66b9674b0d07fb1ac2cb613628`. Deferred build/Roslyn dotnet audit because CPU sampled at 100%.
Rejected Alternatives: Launch `dotnet` above the explicit 50% CPU ceiling or claim stale build proof.
Scalability potential: Runtime unaffected; verification remains static until a legal compiler window opens.
Hardware Impact: Avoided adding compiler pressure to a saturated workstation.

Problem: `ControlRemapper.TryFindBindingsArray` had root-scope intent but asymmetric behavior: it accepted unknown root fields before `bindings`, rejected valid unknown root fields after `bindings`, and could still be mistaken for a cover-to-cover root contract by tests that only checked the happy path.
Solution: Keep `bindings` top-level only, finish the root object after the array with duplicate-`bindings` rejection, reject trailing garbage, and allow non-authoritative future root fields after `bindings` through the same `SkipValue` grammar used before the array.
Rejected Alternatives: Require `bindings` to be the final property forever, or search for `"bindings"` recursively. Final-property strictness blocks harmless future metadata; recursive search accepts spoofed nested arrays.
Scalability potential: Runtime tiers unaffected. The parser remains bounded by `MaxControlsJsonBytes` and `MaxBindingRecords`.
Hardware Impact: Cold load only. Additional root-field scan is bounded and not in a simulation frame.

Problem: A malformed controls record without `id` degraded to `BindingGuidHash64 == 0`, which made `BindingIdMatches` accept by index instead of binding GUID.
Solution: Make `id` mandatory in parsed records and reject duplicate `id` fields. The saved file format already emits `id`; missing it is corruption, not compatibility.
Rejected Alternatives: Preserve index-only fallback. It can apply a user override to the wrong binding after action-map edits.
Scalability potential: Same behavior on weak and high-end devices; input truth is never quality-scaled.
Hardware Impact: Cold load only; one boolean check per record.

Problem: `TryLoadOverrides` validated records before clearing, but a later apply failure still cleared all runtime overrides and returned false, leaving the live session worse than before the load attempt.
Solution: Capture current overrides into a fixed rollback array before clearing, restore them when `applied != recordCount`, and clear rollback references on every exit path.
Rejected Alternatives: Clear and fail without rollback, or allocate a dynamic list for rollback. The first loses user input state; the second adds avoidable managed allocations to the load path.
Scalability potential: Low devices fail closed to previous working controls instead of default or partial controls. Higher tiers keep identical input semantics.
Hardware Impact: Cold failure path only. The bounded rollback loop touches at most 128 records; hot frame 0 us.

Problem: The rollback array is static and contains managed `InputAction`/string references, so any missed cleanup path can retain old runtime graph objects.
Solution: Clear rollback entries after success/failure and again in `finally`. The duplicate clear is intentional idempotent hygiene.
Rejected Alternatives: Rely on branch-local cleanup or switch to per-load managed allocation. Branch-local cleanup misses future early returns; managed allocation violates the contract more directly.
Scalability potential: Runtime behavior unchanged. This reduces stale-reference risk during scene/service churn on all device tiers.
Hardware Impact: Cold load only; zero simulation-frame impact.

Problem: Restoring arbitrary valid saved paths after a process restart requires a managed `string` because Unity `InputAction.ApplyBindingOverride` has a string contract.
Solution: Keep the single `new string` in `TryDecodeControlPathString`, mark it as cold and contract-bound, and keep all hot UI/status paths on cached char buffers. Parser validation still rejects malformed paths before clearing.
Rejected Alternatives: Remove arbitrary path restoration and accept only paths already present in the current binding graph. That breaks legitimate saved overrides after restart, which the regression tests cover.
Scalability potential: Save identity remains the same for low/middle/high/ultra. This is functional input persistence, not visual quality.
Hardware Impact: Cold load allocation per arbitrary restored path. Hot frame 0 us; no menu status text allocation introduced.

Problem: The static rollback snapshot used for transactional `controls.json` load was not protected against concurrent cold load calls.
Solution: Add an `Interlocked.CompareExchange` lease around rollback capture/apply/cleanup and return `ConcurrentOperation` numeric telemetry when the lease is already held.
Rejected Alternatives: Assume all callers are main-thread serialized, allocate a fresh rollback list per call, or use `lock`. Main-thread assumption is not a contract; per-call allocation violates the cold-path austerity goal; `lock` can block instead of fail-closed.
Scalability potential: Weak devices fail immediately instead of contending; high-end devices keep deterministic input semantics. No quality tier changes input authority.
Hardware Impact: Cold load only. One interlocked compare/exchange; hot frame 0 us.

Problem: The ARM64 layout proof only checked representative offsets, and the latest report had stale/manual offset ordering.
Solution: Expand `InputBindingLayoutGuard.Validate()` to check every public field offset in all four DTO structs and refresh the report map from the actual source layout.
Rejected Alternatives: Keep partial guard plus manual report. Manual byte maps drift; partial guards miss accidental field insertion in the unchecked tail.
Scalability potential: Runtime behavior unchanged. This hardens platform-crossing memory proof across low/middle/high/ultra hardware.
Hardware Impact: Editor/cold validation only; no simulation-frame cost.

Problem: Runtime binding override clear was exposed only as a void method, so `RebindingManager` could publish clear/load success even if the native input owner had no runtime asset or rejected the clear.
Solution: Expand `INativeInputManagerRuntime` with `TryClearBindingOverrides()`, implement no-throw false return in `InputManager`, route `ControlRemapper` and `RebindingManager` through bool fail-closed helpers, and keep the old void method as a compatibility wrapper only.
Rejected Alternatives: Leave success-blind `ClearBindingOverrides()` because it is a cold UI path, or mutate the existing void signature. The first lies to UI/persistence; the second would break callers during a parallel batch. Interface expansion is allowed and all local implementers were updated.
Scalability potential: Low devices avoid controls/default-state split-brain when runtime input setup is unavailable; Middle/High/Ultra keep identical input truth. No quality weight changes input authority.
Hardware Impact: Cold settings action only. One bool branch and narrow exception filter; hot frame 0 us.

Problem: A rejected runtime clear during transactional `controls.json` load could still lead to partial mutation or false telemetry if not handled before apply.
Solution: Add a `ControlRemapper` helper that calls `TryClearBindingOverrides()` and converts rejection/narrow Unity exceptions into numeric failure flags before applying saved records. Current overrides remain intact when clear fails.
Rejected Alternatives: Rely on `RemoveAllBindingOverrides` not throwing or let the outer load catch handle it. Outer catch did not cover every Unity clear failure and would not prove state preservation.
Scalability potential: Same deterministic input behavior on weak and high-end devices; fail-closed beats fallback guessing.
Hardware Impact: Cold load only. No simulation-frame work.

Problem: Verification after try-clear contract expansion needed compiler proof, but the first host build gate was closed and the later legal build failed outside the input/UI domain.
Solution: Ran exact-file static scans, native field scan, AUP scan, diff check, and scoped hash `040e8eb74b32e8f17c94e967fadaced26c513e738c16b479c48237c677122a3b`. After a later CPU gate opened at 24.3% with no compiler process, ran one constrained Core build. It failed in non-domain Tools/Gameplay/Audio/Inventory/Buoyancy/World files; no Agent 1332 source file appeared in the errors.
Rejected Alternatives: Launch `dotnet` above the explicit 50% CPU ceiling, edit non-domain compile walls, or reuse stale build proof.
Scalability potential: Runtime unaffected; proof remains static until a legal compiler window opens.
Hardware Impact: Avoided adding compiler pressure to a busy workstation.

Problem: `InputManager.TryClearBindingOverrides()` returned false when `_runtimeInputActionAsset` was null, even though the rest of the input command surface initializes the runtime asset through `EnsureInputActionsInitialized()`.
Solution: Make `TryClearBindingOverrides()` call `EnsureInputActionsInitialized()` before rejecting the clear request.
Rejected Alternatives: Leave early clear/default-load paths dependent on Awake/OnEnable timing, or make `RebindingManager` special-case initialization. Runtime input ownership belongs in `InputManager`.
Scalability potential: Low/Middle/High/Ultra behavior is identical. Input truth is not quality-scaled.
Hardware Impact: Cold clear/default-load only. One initialization check; hot frame 0 us.

Problem: `RebindingManager.ClearOverrides()` could clear runtime overrides and then fail deleting saved files, leaving the current session on defaults while the saved `controls.json` still existed.
Solution: Capture current runtime overrides into a fixed 128-record rollback array, clear runtime, delete saved files, and restore captured overrides on delete failure or rejected clear. Clear rollback references in `finally`.
Rejected Alternatives: Reload from `controls.json` after delete failure, or allocate a dynamic rollback list. Reload is not equivalent when the file is corrupt or stale; dynamic allocation is avoidable in a cold settings command.
Scalability potential: Weak devices keep the previous working controls on I/O failure instead of entering split-brain defaults. Higher tiers keep identical input semantics.
Hardware Impact: Cold reset failure path only. Bounded scan over at most 128 override records; normal simulation frame 0 us.

Problem: `DeleteOverridesFileIfExists()` deleted primary `controls.json` before the temp file and used `&=`, so temp-delete failure could report false after destroying the authoritative save.
Solution: Delete the temp file first and short-circuit before touching the primary save file.
Rejected Alternatives: Treat temp cleanup failure as harmless after deleting the primary file. That loses user input state while returning failure.
Scalability potential: Same deterministic persistence behavior on low/middle/high/ultra hardware.
Hardware Impact: Cold reset only; one branch and reversed deletion order.

Problem: `AccessibilitySettings` allocates GraphicsBuffer instances in `Awake`, but release was only wired through `OnDisable`/service shutdown.
Solution: Add `OnDestroy()` and route it to the same idempotent `OnServiceShutdown()` path.
Rejected Alternatives: Assume every destroy path has a preceding disable after buffer allocation. Unity lifecycle edge cases and disabled-prefab/editor destruction make that assumption unnecessary risk.
Scalability potential: Low hardware avoids leaked constant-buffer objects during scene/UI churn; higher tiers keep the same CBuffer/fallback-vector shader route.
Hardware Impact: Cold teardown only. Hot VisualSync path unchanged.

Problem: Verification after the runtime clear transaction patch needed compiler proof, but the host gate was explicitly closed.
Solution: Ran exact-file static scans, native token/field scan, AUP scan, diff check, and scoped SHA-256 `87a0d6346e68560401b921599202e859e24f4d57d4c8c81b0115a085cd226c5e`. Deferred build/Roslyn audit because a later compiler-process check was clear, but CPU sampled 65/85/79 (avg 76.3%).
Rejected Alternatives: Launch `dotnet` under saturated CPU, or claim stale build proof.
Scalability potential: Runtime unaffected; verification remains static until a legal compiler window opens.
Hardware Impact: Avoided adding compiler pressure to a saturated workstation.

Problem: PDA controls rebinding did not subscribe to conflict detection, so a conflict from PDA could leave `RebindingManager` pending without a visible owner route.
Solution: Add cached PDA conflict subscription, preallocated modal message buffer, and confirm/cancel modal routing matching the pause controls panel.
Rejected Alternatives: Let the pause controls panel be the only conflict UI owner. It is not necessarily active when PDA starts the rebind.
Scalability potential: Low/Middle/High/Ultra input truth remains identical; this is a cold rebind UI route.
Hardware Impact: Normal frame 0 us. One cached delegate and one 256-char buffer per PDA controls component.

Problem: A naive inactive-panel cancellation route can abort another UI owner's rebind because Pause and PDA share the same global binding service.
Solution: Track `_ownsActiveRebind` in each controls panel and cancel only the owning panel's active operation or pending conflict during disable/destroy, inactive input-consumption, PDA close, or PDA tab-away.
Rejected Alternatives: Global cancel from any inactive panel, or no lifecycle cancel. The first breaks sibling UI ownership; the second leaks rebind operations after the visible route disappears.
Scalability potential: Same deterministic input lifecycle on weak and high-end devices. Quality weight does not affect input authority.
Hardware Impact: Cold UI lifecycle only; hot frame 0 us.

Problem: PDA controls refreshed labels/bindings during enable/start/hot-swap while the controls tab was invisible.
Solution: Gate those calls through `RefreshAllIfControlsTabActive()` and keep actual tab open/change as the refresh owner.
Rejected Alternatives: Keep hidden refresh because it is menu-only. It still performs avoidable TMP/InputSystem work on inactive UI.
Scalability potential: Low hardware avoids hidden UI work; higher tiers keep identical input truth.
Hardware Impact: Removes O(row count) inactive-tab refresh work; exact microseconds require Unity Profiler.

Problem: Build proof after the owner-local UI lifecycle patch could not be claimed from previous green passes.
Solution: Static scans and hash proof are current (`e41629794a20c183ddbab53185d731b9b015e534aaace8e03bafb78856beece1`). A pre-correction gated build failed in non-domain files; after correction, no build was launched because CPU reached 52.20%, then 100% with eight dotnet processes and one VBCSCompiler.
Rejected Alternatives: Touch external compile walls, claim green, or launch compiler above the explicit gate.
Scalability potential: Runtime design unchanged; verification remains static until a legal compiler window opens.
Hardware Impact: Avoided competing with active compiler workload.

Problem: Pause and PDA controls panels share one `IInputBindingService`, but their cancel input handlers still called `CancelRebind()` whenever the service reported a rebind, without proving that the current panel started it.
Solution: Gate both `TryHandleCancelSignal()` implementations on `_ownsActiveRebind`; clear stale owner flags when the service is absent or no longer rebinding; leave the cancel result false so caller input routing does not suppress unrelated UI actions.
Rejected Alternatives: Keep global cancel because only one controls panel is expected to be visible. That assumption breaks under PDA/pause overlap, service hot-swap, or future split-screen/editor test harnesses.
Scalability potential: Low-tier devices avoid stuck or hijacked rebind UI without extra allocations. Middle/High/Ultra preserve identical input truth; quality weight does not alter binding authority.
Hardware Impact: One bool branch in cold menu input processing; simulation-frame cost 0 us.

Problem: Rebind lifecycle events are broadcast by the shared service to every subscribed UI panel, so a non-owner active subscriber could render status, conflict modal, or refresh rows for another panel's rebind.
Solution: Set `_ownsActiveRebind` before calling `StartInteractiveRebind()` and require that flag in `HandleRebindStarted`, `HandleRebindCompleted`, `HandleRebindCanceled`, and `HandleConflictDetected`.
Rejected Alternatives: Add owner tokens to `IInputBindingService` events. That would expand a shared Core contract during a crowded batch and force every consumer to migrate; owner-local filtering solves the observed defect inside this domain.
Scalability potential: Weak devices avoid duplicate modal/status work; higher tiers keep deterministic single-owner UX. No gameplay truth or DTO layout changes.
Hardware Impact: One branch per cold rebind event; normal frame cost 0 us.

Problem: On GlobalRegistry `Input` or `InputBinding` replacement, the controls panels unsubscribed before cancelling an owned active rebind on the previous service, leaving a potential orphaned interactive operation with no UI owner.
Solution: Call `CancelOwnedRebindIfNeeded(_subscribedRebindingService)` before `Unsubscribe()` in both `OnGlobalRegistryServiceReplaced()` implementations.
Rejected Alternatives: Assume registry hot-swap happens only outside active menus. The project explicitly supports service replacement listeners; not closing lifecycle under replacement violates the owner-local route.
Scalability potential: Low/Middle/High/Ultra behavior is identical; replacement cleanup remains cold.
Hardware Impact: Cold service swap only; hot frame 0 us.

Problem: The latest owner-filtered UI patch needed proof without violating the build CPU gate.
Solution: Ran exact-file static scans, source-guard checks, native token classification, `git diff --check`, and scoped SHA-256 `2cef7072e666e3a00e28fb3e388b4e448706e1f1df3a3ad5e086aa33fc280c38`. Build was not launched because CPU sampled 100%, then 96% after a 30 second wait.
Rejected Alternatives: Launch `dotnet build` above the explicit 50% ceiling, or claim compile proof from previous passes.
Scalability potential: Runtime unaffected; verification remains static until a legal compiler window opens.
Hardware Impact: Avoided adding compiler pressure to a saturated workstation.

Problem: Automatic rebind save failure was still reported through global `OnOverridesSaveFailed`, so a non-owner active Pause/PDA panel could refresh and show failure for a rebind it did not start.
Solution: Add owner-context event `OnRebindSaveFailed(actionName, actionMap, bindingIndex)`, invoke it before the generic compatibility event, and make Pause/PDA subscribe only to the owner-context route with cached delegates gated by `_ownsActiveRebind`.
Rejected Alternatives: Keep a short-lived UI flag after `OnRebindCanceled`, or keep listening to the global save-failed event. The flag can be consumed by a later unrelated failure; the global event has no owner identity.
Scalability potential: Low avoids duplicate menu refresh/status work; Middle/High/Ultra keep deterministic single-owner rebind UX. Input truth is not quality-scaled.
Hardware Impact: One cold branch per save-failed rebind event; normal simulation frame 0 us.

Problem: `ScalabilityTierProfiles` was a binary Low/High contract even though project policy requires scalable low/middle/high/ultra behavior and continuous `GlobalQualityWeight`.
Solution: Extend the profile byte contract to low/middle/high/ultra, migrate legacy byte `1` to current high, and expose `ToGlobalQualityWeight01` for continuous consumers while preserving old low/high saves.
Rejected Alternatives: Change stored byte values without legacy migration, or leave the binary contract because it is shared Core. The first breaks settings; the second keeps a known policy violation in the input/platform dependency route.
Scalability potential: Low = MX350 survival, Middle = balanced visual budget, High = high RTX profile, Ultra = visual-overkill weight 1.0. All tiers keep identical input/save authority.
Hardware Impact: Cold settings/platform normalization only. No simulation-frame cost; it removes a binary policy fork that would force consumers into low/high-only behavior.

Problem: Build proof after the save-failed/scalability patch cannot be claimed while the explicit host gate is closed.
Solution: Ran static verification and source guards, then checked compiler processes and CPU. Initial checks had no compiler processes, but CPU sampled 57%, 90% after a 30 second wait, then 56%. A later wait sampled 80-83% with `dotnet` pid 19660 and `VBCSCompiler` pid 35324 active, so build was deferred. Scoped source hash: `6ad7e44508940b02773554decb8882d2e87bd62502d3221fe55198b4918f6432`.
Rejected Alternatives: Launch `dotnet build` above the 50% CPU ceiling, or reuse prior compile proof. Both are false verification.
Scalability potential: Runtime behavior unchanged; proof remains static until a legal compiler window opens.
Hardware Impact: Avoided adding compiler pressure to a saturated workstation.

Problem: `ControlRemapper.SkipValue` skipped unknown controls.json values only until the first comma/brace/bracket, so valid future nested metadata after `bindings` could corrupt load compatibility.
Solution: Add `SkipContainerValue` with a stack-only closer table and a 16-level cap. Unknown object/array values are skipped without managed allocation and still reject non-ASCII/control-byte strings.
Rejected Alternatives: Leave future fields primitive-only, or allocate a JSON parser. Primitive-only breaks forward compatibility; managed parser violates the controls.json contract.
Scalability potential: Low/Middle/High/Ultra share the same save identity; future metadata can be ignored without changing input truth.
Hardware Impact: Cold controls.json load only. No simulation-frame cost.

Problem: The parser required `id`, but `id:0` still made `BindingIdMatches` return true and silently degraded to index-only matching.
Solution: Reject `state.BindingGuidHash64 == 0UL` during parse and make `BindingIdMatches` fail closed on zero expected hash.
Rejected Alternatives: Treat zero as legacy wildcard. Missing `id` is already rejected; zero must not become a different bypass route.
Scalability potential: Deterministic input identity remains stable across all device tiers.
Hardware Impact: Cold controls.json load only; one integer comparison per parsed record.

Problem: Build proof after the parser/id patch cannot be claimed while the host gate is closed.
Solution: Ran static verification and source guards. Build was deferred because CPU sampled 58% and compiler processes were active (`dotnet` pid 44004, `VBCSCompiler` pid 24496). After a 30 second wait, `VBCSCompiler` pid 24496 remained active and CPU sampled 60%. Scoped source hash: `8a990f3e8db34c79eba3b1c4e7114fdd9b174e91e3f1b8d5893d373995b7e26c`.
Rejected Alternatives: Launch `dotnet build` above the 50% CPU ceiling or alongside another compiler.
Scalability potential: Runtime behavior unchanged; proof remains static until a legal compiler window opens.
Hardware Impact: Avoided adding compiler pressure to a busy workstation.

Problem: My strict unknown-field JSON skipper for `controls.json` rejected valid raw UTF-8 strings in future metadata, so a localized future field could break old loader compatibility.
Solution: Add allocation-free UTF-8 scalar validation inside `ControlRemapper.SkipJsonStringValue()`, accepting RFC-3629 byte sequences and rejecting overlong encodings, surrogate-range triples, invalid continuation bytes, and code points above U+10FFFF. Added an editor regression that appends a raw UTF-8 future string after `bindings` and proves loading still restores the saved override.
Rejected Alternatives: Keep ASCII-only parsing, or decode unknown future strings to managed `string`. ASCII-only is not future-compatible; managed decode is unnecessary because unknown values only need validation and skipping.
Scalability potential: Low/Middle/High/Ultra save identity remains identical. Future localized metadata can coexist with old binaries without changing input truth.
Hardware Impact: Cold controls.json load only. No simulation-frame cost; skip loop adds bounded byte checks only when an unknown future string exists.

Problem: Compiler proof opened briefly, but the project build state is externally incomplete.
Solution: Ran one scoped Core build after CPU/compiler gate opened; it failed on missing `Temp\CodexBuild\Unity.ShaderGraph.Editor\Unity.ShaderGraph.Editor.dll`. Ran one solution build to let dependencies build in order; it timed out after 184 seconds. Stopped the owned build process and shut down build servers, leaving no `dotnet/csc/VBCSCompiler` processes.
Rejected Alternatives: Claim compile proof, keep retrying builds into a wall, or edit non-domain generated Unity/ShaderGraph build artifacts.
Scalability potential: Runtime behavior unchanged; verification is static until Unity/generated metadata is restored.
Hardware Impact: Build-only cost. Runtime cost remains 0 us in hot simulation frames.

Problem: Pause/PDA conflict handling called the static `ModalWindow.Show` wrapper. That wrapper returns silently when the modal service is absent, leaving `RebindingManager` in pending-conflict/`IsRebinding` state with no visible confirm/cancel route.
Solution: Resolve `IModalWindowService` directly through `GlobalRegistry.ModalWindow` in the cold conflict event. If absent, invoke the cached cancel callback and write a preallocated status message; if present, call `ShowModal` directly with the existing char buffer.
Rejected Alternatives: Reintroduce broad try/catch around modal display, rely on the silent wrapper, or leave the user to press cancel after an invisible conflict. Broad catch hides defects; the wrapper has no success signal; invisible pending state is a UI deadlock.
Scalability potential: Low/Middle avoid stuck controls UI on missing modal prefab/service. High/Ultra keep the same deterministic single-owner rebind UX; no quality tier changes input truth.
Hardware Impact: Cold conflict event only. One registry read and one branch; hot simulation frame cost 0 us.

Problem: `ScalabilityTierProfiles.Normalize()` maps legacy byte 1 to current high byte 3, but two switch statements still contained dead `LegacyHighRtx` cases after normalization.
Solution: Remove the unreachable cases and guard the source in editor tests.
Rejected Alternatives: Keep dead branches as documentation. The legacy behavior is already documented by `Normalize()` and tested; dead branches weaken static proof.
Scalability potential: Low/Middle/High/Ultra behavior unchanged. The continuous profile route remains low=0, middle=2, high=3, ultra=4 with legacy high=1 migrating to high.
Hardware Impact: No measurable runtime delta; this is contract hygiene.

Problem: `AccessibilitySettings` existed as a runtime component but no bootstrap route created it, and a GUID scan found no serialized scene/prefab placement. The color-filter CBuffer could therefore be fully implemented and still never run.
Solution: Give `AccessibilitySettings` an owner-local `ActiveRuntimeInstance`, duplicate suppression, and idempotent shutdown. `GameBootstrapper.InitializePlayerLayerAsync` now creates `[AccessibilitySettings]` after the input/rebinding services and persists it.
Rejected Alternatives: Assume scene authors will add the component manually, or scan the scene every frame. Manual placement is not a contract; hot scene search violates the global owner/read doctrine.
Scalability potential: Low uses vector fallback when CBuffer is unsupported; Middle/High/Ultra keep the CBuffer route and can spend `GlobalQualityWeight` continuously in shader math. Input truth and settings identity are unchanged.
Hardware Impact: One cold GameObject/AddComponent when missing. VisualSync remains a dirty 16-byte CBuffer upload; normal frame cost is unchanged.

Problem: `UserOptionsPersistence.Save()` discarded the disk-write result, so non-domain callers could not verify cold settings persistence without reimplementing the save path.
Solution: Add `TrySave()` and `LastSaveSucceeded`; keep `Save()` as a compatibility wrapper.
Rejected Alternatives: Change all existing callers to a new interface immediately, or leave saves success-blind. Broad caller rewrites would cross multiple domains; success-blind saves hide disk failures.
Scalability potential: Settings save identity is unchanged across Low/Middle/High/Ultra. The new bool route only improves failure visibility.
Hardware Impact: Cold settings path only. Normal simulation frame cost 0 us.

Problem: Legacy `options.h8cfg` JSON migration parsed float values through `Substring`, allocating a managed string even though `float.TryParse(ReadOnlySpan<char>, ...)` can parse the same token.
Solution: Replace the substring token with `json.AsSpan(tokenStart, length)` and add a source guard.
Rejected Alternatives: Leave it because migration is cold. The domain is menu/settings hitch prevention, and this allocation was avoidable without changing the public format.
Scalability potential: No tier behavior change. Old JSON float settings migrate identically.
Hardware Impact: Removes one cold managed string allocation per migrated float option.
