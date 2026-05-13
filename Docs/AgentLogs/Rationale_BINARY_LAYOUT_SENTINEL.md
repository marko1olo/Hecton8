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
