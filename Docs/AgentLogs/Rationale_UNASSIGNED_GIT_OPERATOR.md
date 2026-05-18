# Rationale_UNASSIGNED_GIT_OPERATOR

Status: PENDING VERIFICATION
Domain: Git Operations / Integration Hygiene

Problem: `dotnet build .\Hecton8.slnx` attempted to compile `RealtimeCSG.csproj`, but `Assets/RealtimeCSG` was deleted earlier. The remaining references were stale solution/project/define residue, not live first-party gameplay code.
Solution: Removed the tracked `Hecton8.slnx` project entry and removed `RealtimeCSG*` scripting defines from `ProjectSettings.asset`. Removed the local generated `Assembly-CSharp.csproj` project reference so the current CLI compile can test the working copy before Unity regenerates project files.
Rejected Alternatives: Restoring RealtimeCSG was rejected because the vendor tree was already deleted and no live GUID references were found. Editing scenes, prefabs, or third-party Easy Save defaults was rejected because those are not required to fix the compile wall and would increase blast radius.
Scalability potential: Low/Middle/High/Ultra unchanged; this is build-graph hygiene, not runtime scalability logic.
Hardware Impact: 0 us runtime impact on i3/MX350. Build-time dependency traversal is reduced by one deleted vendor project.

Problem: After the RealtimeCSG compile wall was removed, `SaveStateMerkleTree` failed with duplicate private `Align16(int)` definitions.
Solution: Removed the earlier duplicate helper and kept the later inlined `Align16(int)` plus existing `Align16(long)` overload used by block alignment.
Rejected Alternatives: Renaming one helper was rejected because it would create redundant call paths. Editing save payload structs or file format constants was rejected because the compiler error was a duplicate private symbol, not a binary layout issue.
Scalability potential: Low/Middle/High/Ultra unchanged; this is compile hygiene.
Hardware Impact: 0 us runtime impact on i3/MX350. No hot path logic added.

Problem: Unity-generated firstpass/editor project files still pulled `RealtimeCSG.csproj` even after tracked `Hecton8.slnx` cleanup, causing 216 missing-source errors from a deleted vendor tree.
Solution: Removed stale `ProjectReference Include="RealtimeCSG.csproj"` entries from ignored generated Assembly-CSharp project files and replaced ignored generated `RealtimeCSG.csproj` with a no-source local stub so CLI verification can pass until Unity regenerates project files from the cleaned ProjectSettings.
Rejected Alternatives: Restoring RealtimeCSG was rejected because live assets are absent and no active scene/prefab GUID dependency was found. Deleting unrelated generated project files was rejected because it would disturb other agents' IDE/build state.
Scalability potential: Low/Middle/High/Ultra unchanged; no runtime code path exists.
Hardware Impact: 0 us runtime impact on i3/MX350. Build graph no longer spends time failing on absent vendor source files.

Problem: `GlobalWorldSampler.FromDataVaultAliases` tripped C# definite-assignment analysis on a field-by-field initialized `GlobalWorldSamplerData` local.
Solution: Initialize the local with `default` before explicit field assignments, preserving struct layout and sampler math while giving new reserve/config fields a deterministic zero baseline.
Rejected Alternatives: Reordering fields or changing `GlobalWorldSamplerData` layout was rejected because it risks job binary layout and ARM64 alignment. Removing reserved fields was rejected because it would weaken forward-compatible telemetry/config padding.
Scalability potential: Low/Middle/High/Ultra unchanged; Math LOD behavior and `GlobalQualityWeight` remain intact.
Hardware Impact: Effectively 0 us on i3/MX350; one cold struct initialization in a setup factory, not a per-sample hot path.

Problem: Current compile pass failed in `LocRegistry.cs` because a parallel localization edit added `[MethodImpl(MethodImplOptions.AggressiveInlining)]` without importing `System.Runtime.CompilerServices`.
Solution: Added the missing namespace only. The existing Babel/DataVault localization logic, DTO layout, telemetry ring, and hot-path lookup code were not changed.
Rejected Alternatives: Rewriting the localization block or reverting the large existing `LocRegistry` diff was rejected because that work belongs to another active agent and the compiler wall was a single missing namespace. Editing `H8Memory.cs` after a transient backtick error was rejected because raw byte/readback showed valid CRLF C# and the next compile passed without touching it.
Scalability potential: Low/Middle/High/Ultra unchanged; no quality-tier behavior changed.
Hardware Impact: 0 us runtime impact on i3/MX350. Compile-only namespace resolution; no allocation, branch, or job scheduling change.

Problem: Earlier full solution output showed construction/player-builder type resolution errors around `ConstructionRequestDTO`, `StructuralBoundsDTO`, `ConstructionValidationSettingsDTO`, `ConstructionSipBudgetDTO`, and `MockWorldSampler`.
Solution: Read back `ModularBaseConstructionValidator.cs`, `PlayerBuilder.cs`, `Directory.Build.targets`, and `Hecton8.Core.csproj`. The DTOs and namespace-level `Hecton8.Construction.MockWorldSampler` exist, and the validator is already included in `Hecton8.Core.csproj`. No new dependency was added; the current compile confirmed the dependency graph resolves.
Rejected Alternatives: Inventing duplicate construction DTOs, adding a Habitat hard reference to `PlayerBuilder`, or broad-editing `Directory.Build.targets` was rejected because the concrete types already exist and direct cross-domain coupling would violate the parallel-agent boundary.
Scalability potential: Low/Middle/High/Ultra unchanged; construction validation quality weighting remains in the existing validator.
Hardware Impact: 0 us runtime impact on i3/MX350. Static dependency verification only.

Problem: The ultra-polish pass found three construction validator jobs using bare `[BurstCompile]` and several disjoint Native containers without explicit alias metadata.
Solution: Applied the mandated Burst flags `CompileSynchronously = true, FloatMode = Fast, FloatPrecision = Standard` and marked independent Native fields with `[NoAlias]` in `ModularBaseConstructionValidator`.
Rejected Alternatives: Rewriting construction validation math or adding new cross-domain references was rejected because the compile wall was metadata drift, not behavior drift. Interface-array dispatch was not introduced.
Scalability potential: Low/Middle/High/Ultra unchanged; existing continuous quality weighting remains in the validator. The metadata gives Burst a cleaner path to NEON/AVX vectorization.
Hardware Impact: Expected low-end gain is compile/vectorization opportunity only; no reliable microsecond claim without profiler. Runtime allocation remains 0.

Problem: The save pager had been partially migrated from private persistent containers to `GlobalDataVault`: fields were `VaultBufferHandle`, but stale local `NativeQueue`/`NativeParallelHashMap` symbols still controlled allocation, dequeue, and read-result commit.
Solution: Finished the DataVault route using existing `BufferID.SaveWorldPagerWriteCommands`, `SaveWorldPagerReadCommands`, and `SaveWorldPagerReadResults`; queue staging now uses fixed NativeArray ring cursors, and completed read tickets use a bounded 16-slot result scan.
Rejected Alternatives: Reintroducing `_writeQueue`, `_readQueue`, or `_readResults` fields was rejected because it violates the Vault Law and recreates fragmented private persistent state. A dynamic hash map was rejected because the ticket result capacity is already fixed at `ReadSlotCount`.
Scalability potential: Low/Middle/High/Ultra unchanged in visuals. The data path scales predictably: write/read queues are O(1); result lookup is O(16) bounded, so thermal devices do not pay unbounded lookup costs and high-end hardware has no extra branchy container overhead.
Hardware Impact: Removes three private persistent native allocations from pager ownership. Estimated low-end effect is stability and allocator-pressure reduction; per operation, result lookup is at most 16 struct compares, not a heap/container traversal.

Problem: Save AUP quantization contained two variable-name regressions that broke compile and would have corrupted local-sector delta math if force-fixed incorrectly.
Solution: `QuantizeAupSectorHalf3` now subtracts the computed `sectorOrigin`; the overload accepting `sectorOriginMeters` subtracts that parameter before narrowing to local `float3`.
Rejected Alternatives: Changing sector key derivation, DTO layout, or half/local quantization format was rejected because the defect was a name mismatch, not a format flaw.
Scalability potential: Low/Middle/High/Ultra unchanged; this preserves the AUP local-space rule for all hardware tiers.
Hardware Impact: 0 us meaningful runtime change. Correct local subtraction prevents far-world float jitter; no extra branches or allocations added.

Problem: The working tree is shared with many active agents and contains large unrelated tracked/untracked diffs, so a broad commit would claim other domains' work.
Solution: Verified with minimal `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 /nr:false -v:minimal`; it succeeded in 6.10s with 0 warnings and 0 errors. Commit was deferred because safe staging scope is ambiguous under current dirtiness.
Rejected Alternatives: Full solution rebuild and broad `git add -A` were rejected. Full rebuild is unnecessary for the touched compile wall; broad staging would mix unrelated agent output.
Scalability potential: Low/Middle/High/Ultra unchanged; verification scope only.
Hardware Impact: 0 us runtime. Build-time evidence only.

Problem: `H8BinaryWorldPager.DisposeNativeState()` used `ReleaseOwnerBuffers(SystemID.SavePersistence)`. The pager and `SaveStateMerkleTree` share that owner, so disposing one pager could release unrelated save-persistence vault lanes.
Solution: Removed the owner-wide release path from pager disposal. The pager now clears only its resolved transient buffers, resets its own `VaultBufferHandle` fields, and zeroes pager-local counters/cursors.
Rejected Alternatives: Adding a new owner enum was rejected because it touches shared contracts during a dirty multi-agent batch. Keeping owner-wide release was rejected because it violates dependency isolation and can invalidate peer save buffers.
Scalability potential: Low/Middle/High/Ultra unchanged visually. Save persistence remains stable under streaming churn instead of paying recovery/reallocation after an unrelated pager dispose.
Hardware Impact: 0 us steady-state on i3/MX350; shutdown/dispose path avoids accidental vault churn and cache-cold buffer reacquisition.

Problem: Player movement telemetry structs still used `StructLayout(Pack = 1)`, creating ARM64 unaligned DTO risk in black-box/native lanes.
Solution: `CinematicFocusTelemetryEntry` was reordered to 8-byte fields first and padded to 88 bytes; `RenderInterpolationState` was padded to 40 bytes; managed callback structs dropped `Pack = 1` and use natural sequential layout.
Rejected Alternatives: Keeping byte-packed structs was rejected because Quest/ARM64 can pay unaligned access penalties. Expanding the player movement architecture was rejected; only layout metadata and padding changed.
Scalability potential: Low/Middle/High/Ultra unchanged. The same telemetry survives cheap-device alignment constraints and high-end bulk copy paths.
Hardware Impact: No logic-time gain claimed. The important effect is removing unaligned-copy risk for ARM64 and keeping DTO sizes multiples of 8.

Problem: The next Core compile exposed two integration walls: stale `CacheLineInt` counter call sites in `H8BinaryWorldPager` and `ModCommandDispatcher` referencing `MemorySentinelRuntime` while the existing runtime file was absent from `Hecton8.Core.csproj`.
Solution: Verified current pager counters route `Volatile`/`Interlocked` through the padded `.Value` int field and added the existing `Assets/_Project/Scripts/Core/MemorySentinelRuntime.cs` compile include beside MemorySentinel contracts.
Rejected Alternatives: Replacing `CacheLineInt` with raw `int` was rejected because it removes false-sharing padding. Inventing a ModCommandDispatcher no-op facade was rejected because the real runtime exists on disk. Running a full solution rebuild while other `dotnet` builds were active was rejected.
Scalability potential: Low/Middle/High/Ultra unchanged. False-sharing padding preserves multi-core behavior; MemorySentinel remains the existing scalable validation system rather than a fallback stub.
Hardware Impact: 0 us runtime from the csproj include. Counter padding preserves intended 64-byte cache-line isolation; exact microsecond delta requires profiler data.

Problem: The final Core compile succeeded but reported eight `CS0649` warnings in `GlobalPhysicsStateManager.PhysicsDistanceCullingJob` legacy fields after another agent's `PhysicsDistanceCullingJobShinobu37` replacement.
Solution: Read the scheduling path: only `PhysicsDistanceCullingJobShinobu37` is scheduled. I did not suppress or delete the legacy private job in this pass because `GlobalPhysicsStateManager.cs` is already under large active edits by another agent.
Rejected Alternatives: Warning-pragmas were rejected as cosmetic. Removing the legacy struct was rejected without owner confirmation because it sits inside an active physics rewrite and would increase merge risk.
Scalability potential: The active Shinobu37 path already receives continuous culling distances, frustum data, AUP-local camera state, and hardware scale; no binary quality branch was added.
Hardware Impact: 0 us runtime change from this decision. Residual warning cleanup remains a physics-domain integration task, not a save/build hygiene hot fix.

Problem: The repository is heavily dirty with parallel agent work; several tempting files (`ThermalGeyser`, `HectonNetworkManager`, rollback runtime, volcanic updraft, MemorySentinel runtime) are dependency-coupled outside this save/player capsule.
Solution: Staged only `H8BinaryWorldPager`, the minimal `H8Memory` BufferID/SystemID additions required by it, player telemetry layout fixes, and the UNASSIGNED_GIT_OPERATOR evidence logs. Verified staged names and whitespace before commit.
Rejected Alternatives: `git add -A` was rejected because it would capture unrelated agents' work. Staging `ThermalGeyser.cs` alone was rejected because it depends on untracked `VolcanicUpdraftDirector`; staging `HectonNetworkManager.cs` alone was rejected because it depends on untracked rollback runtime/contracts.
Scalability potential: Low/Middle/High/Ultra unchanged; this is source-control isolation preserving compile-wall boundaries.
Hardware Impact: 0 us runtime. Prevents dependency breakage from partial cross-domain commits.

Problem: The user explicitly requested git commit and push, but the post-commit worktree remains dirty with unrelated active-agent files.
Solution: Pushed only scoped commit `23c5b933d` to `origin/main` and left unrelated local modifications unstaged. A docs-only follow-up records the push result.
Rejected Alternatives: Pull/rebase gymnastics were not needed because the push was fast-forward. Force-push was not considered.
Scalability potential: Low/Middle/High/Ultra unchanged; source-control evidence only.
Hardware Impact: 0 us runtime.

Problem: The active dirty `H8Memory.cs` registry accumulated cross-agent buffer ranges that collided: `HullIntegrity*` overlapped `VerletCable*` on `575..584`, and `ConstructionPreview*` overlapped the already-pushed `SaveWorldPager*` IDs on `70200..70202`.
Solution: Kept `VerletCable*` on `575..586`, kept `SaveWorldPager*` on `70200..70209`, moved `HullIntegrity*` to the free `70080..70089` block, and moved `ConstructionPreview*` to free `70320..70322`. A focused enum scan now reports no duplicate `BufferID` values in the working `H8Memory.cs`.
Rejected Alternatives: Moving save pager IDs was rejected because those IDs are already pushed and consumed by `H8BinaryWorldPager`. Broad `git add -A` was rejected because the tree has 1027 dirty entries. Starting `dotnet build` was rejected at this step because CPU snapshot was 60%, above the local no-build threshold.
Scalability potential: Low/Middle/High/Ultra unchanged visually. The value is registry determinism: DataVault handles no longer alias two unrelated buffers to one integer.
Hardware Impact: 0 us runtime. Prevents memory-lane identity corruption; no hot-path math changed. Pushed as `7110cdceb`.

Problem: The tracked project graph still had a local RealtimeCSG cleanup waiting in the dirty tree: `Hecton8.slnx` removes `RealtimeCSG.csproj`, and `ProjectSettings.asset` removes stale `RealtimeCSG*` scripting symbols while `Assets/RealtimeCSG` is absent.
Solution: Keep the cleanup scoped to tracked graph/define files only and prepare it as its own git capsule. The ignored generated `RealtimeCSG.csproj` stub remains local CLI insulation until Unity regenerates project files, but it is not source truth and is not staged.
Rejected Alternatives: Restoring the deleted RealtimeCSG vendor tree was rejected because no live first-party source depends on it. Staging Easy Save defaults, profiler traces, or ignored generated `.csproj` files was rejected because they are either vendor/editor residue or local generated graph state. Touching the untracked Core/Atmosphere `WaterlineBreachSignal` packet was rejected in this capsule because it belongs to a separate domain handoff and would require committing a large untracked runtime bundle.
Scalability potential: Low/Middle/High/Ultra unchanged; this removes dead compile/define surface only.
Hardware Impact: 0 us runtime. Build-time graph traversal no longer points at a deleted vendor project, and platform defines stop advertising a package that is absent on disk.
