# Rationale_BINARY_LAYOUT_SENTINEL

Status: PENDING VERIFICATION

## Initial Scope

Problem: Unsafe blitting across IL2CPP ARM64/x64 can corrupt SaveData, MMF, AUP, voxel RLE, and telemetry records when structs rely on implicit layout.
Solution: Build a cold-boot binary layout manifest, annotate critical DTOs/jobs with deterministic layout attributes, and gate unsafe MemCpy through an explicit safety marker.
Rejected Alternatives: Runtime reflection in hot paths and broad serializer replacement. Both violate zero-GC and batch scope.
Scalability potential: Low uses cold boot validation only; Middle adds complete manifest coverage; High/Ultra can expand manifest export tooling without runtime cost.
Hardware Impact: Expected low-end i3/MX350 runtime gain is stability, not frame savings. Cold boot validation cost is outside gameplay frame budget.

## Loop 1 Decisions

Problem: A custom `[BinaryBlittableSafe]` marker was required without turning Core memory layout into a dependency knot.
Solution: Created `Hecton8.Core.Memory.Layout` as a no-reference, no-engine asmdef containing only the marker attribute.
Rejected Alternatives: Putting the attribute in `Hecton8.Core` would let every domain depend on Core for a marker; hard-coding the marker inside `MemoryInquisitor` would keep layout policy coupled to unsafe copy logic.
Scalability potential: Low/Middle/High/Ultra all pay zero frame cost; Ultra can add editor exporters around the same marker without changing runtime.
Hardware Impact: 0 us/frame on i3/MX350; avoids ARM64/x64 layout ambiguity before save/MMF data is touched.

Problem: `MemoryInquisitor` needed to reject unsafe blit types without reflection in gameplay.
Solution: Added a generic static attribute cache and prewarm through `BinaryLayoutManifest` during cold boot.
Rejected Alternatives: Reflection on every blit call was rejected as a hot-path GC risk; a manual whitelist was rejected because cross-domain types would force direct dependencies.
Scalability potential: Low uses prewarmed static bool checks; High/Ultra can expand manifest coverage with no hot-path penalty.
Hardware Impact: Expected hot-path cost is one cached static bool branch; measured proof absent.

Problem: The prompt required a 5-byte RLE DTO, but the active rich voxel RLE record stores material and flags in 8 bytes.
Solution: Added `SaveVoxelDeltaRun5` as exact `ushort, byte, ushort` / `Pack = 1, Size = 5`; retained `SaveVoxelDeltaRun8` because deleting material/flag bytes would corrupt active voxel material state.
Rejected Alternatives: Replacing `SaveVoxelDeltaRun8` outright was rejected because current `VoxelDeltaProcessor` reads `MaterialId` and `Flags` during load.
Scalability potential: Low can use the 5-byte SDF-only payload when material/flags are default; High/Ultra can keep richer material payloads where visual detail matters.
Hardware Impact: Potential save/MMF payload reduction is 3 bytes per SDF-only run; no measured runtime frame gain.

Problem: AUP binary layout must survive IL2CPP ARM64/x64 and large-world save paging.
Solution: Verified AUP and AUP blit DTOs at 48 bytes with explicit offsets and 16-byte multiples; legacy 36-byte AUP remains isolated in the v7 migration DTO.
Rejected Alternatives: Expanding current AUP to 64 bytes was rejected because it would break v8 payload offsets and existing save migration constants.
Scalability potential: Low uses compact 48-byte authority; Ultra can add 64-byte high/low GPU transfer DTOs separately.
Hardware Impact: No frame gain; prevents data corruption in AUP save/MMF lanes.

Problem: Boot failure needed objective postmortem data, not a chat-only claim.
Solution: Manifest failures publish `ComplianceViolationSignal` and dump `Dump_BINARY_LAYOUT_SENTINEL.bin` with struct name, expected value, observed value, and context hash.
Rejected Alternatives: `Debug.LogError` only was rejected because release/dev boot needs binary evidence.
Scalability potential: Same behavior across tiers; higher tiers can add richer sidecar tools without changing the failure ABI.
Hardware Impact: Failure path only; no gameplay cost.

## Loop 2 Decisions

Problem: The manifest asserted `ComplianceViolationSignal` through the same `[BinaryBlittableSafe]` gate used by persistence DTOs, but the signal was missing the marker during the first pass.
Solution: Added the marker to the existing explicit 32-byte signal and imported the no-engine layout assembly in `GlobalSignals`.
Rejected Alternatives: Special-casing signals in `MemoryInquisitor` was rejected because the gate must be uniform; skipping signal validation was rejected because boot failures need deterministic binary evidence.
Scalability potential: Low/Middle/High/Ultra all share the same 32-byte signal lane; Ultra can add richer sidecar dump readers without changing the signal ABI.
Hardware Impact: 0 us/frame on i3/MX350; cold failure path can enqueue one fixed-size native signal.

Problem: Full compile verification could not be completed after the final marker patch.
Solution: Attempted a direct `dotnet build` with no restore and single worker; stopped only the child MSBuild nodes created by that timed-out command. Unity MCP validation returned `no_unity_session`.
Rejected Alternatives: Reporting a clean compile without Unity/Bee completion was rejected. Reverting unrelated active work by other agents was rejected.
Scalability potential: Blocker is integration-state only; runtime layout design still scales from low-end cold checks to high-end manifest expansion.
Hardware Impact: No runtime impact. Verification wall cost was editor/build-machine time only.

## OMEGA POLISH CHANGES

Problem: Polish mandate required anti-bloat review for the sentinel implementation after all tasks were checked or blocked.
Solution: Re-scanned task-owned implementation for `LayoutKind.Auto`, managed `foreach`, `string.Format`, interpolated strings, `.ToString()`, `math.sqrt`, and unconditional `math.normalize`. Sentinel-owned additions did not require a LUT, triangle wave, reciprocal rewrite, or bitmask rewrite because they are cold-boot layout checks and failure-only dump paths. Existing string interpolation hits in `PersistentWorldRegistry` and `FoveatedSimulationManager` predate this sentinel pass and were not modified.
Rejected Alternatives: Editing unrelated runtime logging in files touched by other agents was rejected as cross-domain churn; converting manifest reflection to hot-path static tables was rejected because the reflection is cold boot only and the current manifest needs offset proof.
Scalability potential: Low tier pays one cold manifest pass; Middle/High/Ultra can extend manifest coverage without touching gameplay frame cost. No "balanced" mid-path exists; the data contract is either verified or boot is stopped.
Hardware Impact: 0 us/frame on i3/MX350. Cold boot verifier remains outside gameplay frame budget; no measured microsecond savings.

Final Git Diff: Current sentinel-owned code diff is one deliberate hardening deletion in `BinaryLayoutManifest`: the private `_packedQuantityAndFlags` offset assertion was removed. Public `PersistentWorldItemRecord.InstanceUid` offset at 200 and total record size still prove the 196-byte packed field slot indirectly.
Status: PENDING due global compile/session dependencies, not VERIFIED MASTER GRADE.

## Loop 6 Private Metadata Hardening

Problem: `BinaryLayoutManifest` asserted a private `PersistentWorldItemRecord._packedQuantityAndFlags` field by string name. That is brittle under IL2CPP/private metadata stripping and can create a false cold-boot layout failure even when the binary record is valid.
Solution: Removed the private-field `Marshal.OffsetOf` assertion and retained public offset checks around it: `ItemPersistentId` at 68, `InstanceUid` at 200, and total `PersistentWorldItemRecord` size at 208. This still proves the packed 4-byte slot at 196 without private reflection.
Rejected Alternatives: Making the packed field public was rejected because it would leak mutation surface into persistence callers. Keeping private-name reflection was rejected because the verifier must be stricter about ABI than about private CLR metadata availability.
Scalability potential: Low/Middle/High/Ultra all remain cold-boot only; no frame-tier path changes. Ultra can add an editor-only manifest generator later if exact private-field evidence is required.
Hardware Impact: 0 us/frame on i3/MX350. The gain is false-positive removal on IL2CPP boot, not measured frame time.

## Loop 6 Verification Update

Problem: Unity MCP telemetry responded, but script validation, console reads, and refresh returned `no_unity_session`.
Solution: Ran the Unity Roslyn compiler directly against `Library\Bee\artifacts\1900b0aEDbg.dag\Hecton8.Core.rsp` with codexcheck outputs. Current sentinel-owned files produced no compiler errors; compile now stops on non-sentinel systems.
Rejected Alternatives: Chasing Atmosphere/Audio/Save/UI/Fauna runtime errors was rejected as domain creep under the batch boundary. Claiming full verification was rejected because global compile still fails.
Scalability potential: Sentinel runtime scalability remains unchanged: cached hot gate, cold manifest, failure-only dump.
Hardware Impact: 0 us/frame. Verification cost was editor/build-machine time only.
