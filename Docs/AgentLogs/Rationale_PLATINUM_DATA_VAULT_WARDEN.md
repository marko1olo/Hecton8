# Rationale: PLATINUM_DATA_VAULT_WARDEN

Status: PENDING - VAULT LOCK VERIFIED, COMPILE TARGET BLOCKED

## Decision Log

Problem: Batch prompt requires DataVault and DTO lockdown before implementation.
Solution: Read AGENTS.md, domain map, exact XML prompt, and relevant .agents-skills mandates before any source edit.
Rejected Alternatives: Direct code edits without authority scan; standard Unity mutable ScriptableObject/data-object approach because this task targets binary ABI and native memory.
Scalability potential: Low keeps DTO and vault memory compact; Middle keeps deterministic layout; High and Ultra preserve saved CPU/GC budget for richer presentation systems outside Core.Memory.
Hardware Impact: On i3/MX350, avoiding live relocation and managed hot-path allocations prevents frame stalls and corrupt alias reads; exact microseconds remain PENDING VERIFICATION until compile/profiling evidence exists.

Problem: First-hour save DTO names requested by the batch did not exist as explicit ABI-locked structs.
Solution: Added PlayerKinematicStateDTO (48 bytes), InventoryShadowDTO (32 bytes), and HabitatFloodStateDTO (32 bytes) with Sequential Pack=1 and BinaryBlittableSafe, then added BinaryLayoutManifest size/offset assertions and v72 payload serialization.
Rejected Alternatives: Relying on PlayerStatsDTO, ModuleDTO, or transient inventory payload fields because managed bool/string/array members are not stable ARM64 binary contracts.
Scalability potential: Low keeps save/load mirrors compact and deterministic; Middle can compare mirrors during QA; High and Ultra can spend the saved crash-debug budget on richer first-hour presentation.
Hardware Impact: i3/MX350 hot-frame cost is 0 us; save-time mirror refresh is cold-path and bounded by 256 habitat modules. Manifest checks add estimated <4 us at cold boot.

Problem: Vault aliases could silently refresh after generation drift, hiding stale pointer bugs.
Solution: Preserved the handle API, exposed GenerationID by name, and changed ResolveBuffer to throw FatalMemoryException when a non-default handle generation/pointer/length/stride no longer matches metadata.
Rejected Alternatives: Returning raw NativeArray only; silent refresh; hard coupling alias users to defrag internals.
Scalability potential: Low devices avoid corrupt reads during stress; Middle can assert handle lifetimes; High and Ultra can layer richer telemetry because generation is globally exposed.
Hardware Impact: Valid resolve path adds branch comparisons only, estimated <0.05 us per resolve on i3/MX350; fault path allocates exception by design.

Problem: H8Memory raw frees were pointer-only and allowed wrong owners to unregister allocations.
Solution: Added FatalMemoryException and owner-checked FreeRaw(pointer, allocator, SystemID), then updated DataVault and HectonArenaAllocator call sites with explicit owners.
Rejected Alternatives: Logging and continuing; post-frame leak sweep only; relying on NativeMemorySentinel after damage.
Scalability potential: Low prevents silent native pool corruption; Middle records deterministic failure; High and Ultra keep memory-budget telemetry trustworthy under heavier visual systems.
Hardware Impact: Free path scans existing sentinel records as before with one owner comparison, estimated <2 us for current pool sizes; hot frames without frees pay 0 us.

Problem: Live FrostTickDefrag relocation could move buffers while concurrent agents exposed raw aliases.
Solution: Removed the memmove job, compaction slice, relocation metadata writes, and live UnsafeUtility.MemMove from GlobalDataVault. FrostTickDefrag now analyzes gaps and records pending move telemetry only.
Rejected Alternatives: Smaller relocation budget, locked-buffer-only relocation, or stress-gated memmove; all still permit pointer invalidation.
Scalability potential: Low gets deterministic stability; Middle gets fragmentation telemetry; High and Ultra can schedule future visual loading-mask compaction outside gameplay frames.
Hardware Impact: Removes up to the old 1 ms relocation slice on i3/MX350; exact measured save is blocked by unrelated project compile failures.

Problem: First-hour flood DTO serialization originally risked a save-time temporary array refresh.
Solution: The writer now emits HabitatFloodStateDTO records directly from existing ModuleDTO/ModuleBlitDTO data, preserving the packed payload without creating a temporary managed array.
Rejected Alternatives: Allocating ConstructionDTO.habitatFloodStates on every save; trusting prefilled mirrors.
Scalability potential: Low avoids save hitch allocation; Middle keeps deterministic replay data; High and Ultra can increase module counts later without changing ABI.
Hardware Impact: 0 B GC on the write path for the added DTO block; each habitat record is one 32-byte struct write.

Problem: Black-box defrag telemetry lacked vault generation state.
Solution: Added VaultGenerationID to MemoryDefragTelemetryEntry and populated it on every RecordDefragBlackBox call from the vault generation counter.
Rejected Alternatives: Publishing managed signal telemetry or writing generation only on dump.
Scalability potential: Low gets postmortem alias/generation correlation; Middle/High/Ultra can increase telemetry consumers without changing the native dump record pattern.
Hardware Impact: Native circular entry grows by 4 bytes; 300 frames cost 1200 bytes total and 0 B GC.

Problem: The mandated build command targets Hecton8.Core.Memory.rsp, but the target file is absent.
Solution: Ran the exact command and recorded MSB1009. Ran broader Hecton8.Core.csproj with edited-file filtering; no diagnostics in edited files appeared before unrelated missing-domain compile failures.
Rejected Alternatives: Creating a fake .rsp project or reporting compile green without an executable target.
Scalability potential: Low/Middle/High/Ultra blocked equally until build graph is restored.
Hardware Impact: No runtime impact; validation is blocked by missing build infrastructure, not by memory code execution.

## OMEGA POLISH CHANGES

Problem: Polish audit required removal of fake precision, managed iteration/string debt, and any code outside the DataVault domain without justification.
Solution: Re-read OMEGA_POLISH after checklist completion. Replaced the temporary-array flood DTO write with direct 32-byte struct writes from existing module data. Re-ran scoped scans: no added managed foreach, string interpolation, string.Format, sqrt, or normalize in touched scope. The only scoped ToString hit is the pre-existing cold `DateTime.Now.ToString("O")` in SaveData.CreateNew.
Rejected Alternatives: Broad project cleanup outside this domain; fake Hecton8.Core.Memory.rsp project; retaining any live defrag memmove for "future optimization".
Scalability potential: Low writes stable compact DTOs without temporary garbage; Middle keeps deterministic binary save mirrors; High and Ultra can spend saved memory safety budget on richer non-Core visuals after build graph repair.
Hardware Impact: Direct habitat DTO write is 32 bytes per module and 0 B GC. Removed live relocation keeps the former 1 ms defrag budget available for visible work.

Final Git Diff Summary:
- Assets/_Project/Scripts/Core/HectonArenaAllocator.cs: owner-tagged H8Memory.FreeRaw release.
- Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs: GenerationID handle exposure, stale-handle fatal path, VaultGenerationID telemetry, owner-tagged macro/vault frees, macro copy switched to MemCpy, live defrag memmove code deleted.
- Assets/_Project/Scripts/Core/Memory/H8Memory.cs: FatalMemoryException plus owner-checked FreeRaw.
- Assets/_Project/Scripts/SaveBinaryPayloadCodec.cs: v72 first-hour DTO payload write/read, direct habitat flood struct loop.
- Assets/_Project/Scripts/SaveData.cs: first-hour DTO mirrors and packed DTO definitions/metadata.
- Assets/_Project/Scripts/Core/BinaryLayoutManifest.cs: first-hour DTO size/offset assertions.
- Docs/Tasks/Status_PLATINUM_DATA_VAULT_WARDEN.md and this rationale log updated.
