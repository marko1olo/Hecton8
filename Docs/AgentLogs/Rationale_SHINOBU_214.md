# SHINOBU_214 Rationale

Date: 2026-05-20
Status: STATIC POLISH + ISOLATED_ASMDEF + BLACKBOX + STRUCT_REQUEST_FIX + AXIS_DIMENSION_FIX + QUALITY_CURVE + CSV_PROFILE_AUTHORITY + PROPERTY_PURGE + BATCH_FAULT_ADVANCE + ACCESSOR_PURITY_GUARD + ATLAS_PATH_DRIFT_PRUNED APPLIED / COMPILE BLOCKED BY CPU GATE

## Decision 001: Offline-Only PBR Mask Authority

Problem: AO, Roughness, and Metallic source maps can create three independent texture sampler reads per material, wasting bandwidth and sampler slots on MX350/mobile-class GPUs.
Solution: Build an Editor-only ARM mask packer with R=AO, G=Roughness, B=Metallic, A=255. Runtime receives immutable baked texture assets only.
Rejected Alternatives: Runtime texture compositing, runtime `Texture.Compress()`, material-side macro texture sample, and per-material CPU conversion were rejected because they create frame spikes or extra sampler pressure.
Scalability potential: Low uses compressed ARM masks and baked AO only; Middle uses the same masks with lower mip bias; High uses higher source resolution and stronger preserved roughness; Ultra spends saved bandwidth on richer near-field shader detail without adding sampler count.
Hardware Impact: Expected gain on i3/MX350 is lower texture bandwidth and fewer material texture bindings. Exact microseconds require Frame Debugger/Profiler proof; current state is PENDING VERIFICATION.

## Decision 002: Explicit DTO Layout

Problem: Texture packer job config must be stable across x64 and ARM64 development hardware.
Solution: Define `TexturePackerConfigDTO` with `[StructLayout(LayoutKind.Explicit, Size = 16)]` and raw unmanaged fields: `float NormalIntensity`, `float RoughnessScale`, `float MetallicScale`, `uint Flags`.
Rejected Alternatives: auto-properties, sequential layout without offset asserts, and runtime `bool` flags were rejected due to defensive copies and platform layout ambiguity.
Scalability potential: Same DTO feeds all quality profiles; `GlobalQualityWeight` remains a continuous input in profile math rather than binary output tiers.
Hardware Impact: Avoids unaligned job configuration loads on ARM64. Runtime cost is 0 us because the packer is Editor-only.

## Decision 003: Baked Macro Noise Instead Of Runtime Macro Sampler

Problem: Large 100 km/AUP-scale surfaces expose texture tiling if all variation comes from repeated UV detail maps.
Solution: Inject deterministic low-frequency macro noise offline into AO/Roughness channels based on authored tile scale and profile weight.
Rejected Alternatives: Adding a runtime macro texture sampler or runtime world-space noise in every material was rejected because the task goal is sampler elimination and bandwidth reduction.
Scalability potential: Low receives subtle low-frequency baked variance; Middle/High/Ultra can use stronger source resolution and preserved mip roughness without changing runtime sampler count.
Hardware Impact: Saves one likely macro sampler on low-end silicon while preserving visual breakup. Exact microseconds require material/frame proof.

## Decision 004: ARM Shader Contract Break

Problem: Existing UberNoir shader interpreted `_MaskMap` as legacy ORM: R=Metallic, G=AO, B=Smoothness. New prompt requires ARM: R=AO, G=Roughness, B=Metallic.
Solution: Rename local shader sample to `armSample`, change low and high material paths to read AO from R, roughness from G, metallic from B, and keep A as emission/bio mask.
Rejected Alternatives: Dual shader compatibility branch, material keyword switch, and keeping legacy ORM were rejected because they preserve ambiguity and consume authoring time with two incompatible masks.
Scalability potential: Low still discards metallic in `_MATH_LOD_LOW`; Middle/High/Ultra reuse the same single packed mask and spend saved sampler budget elsewhere.
Hardware Impact: Converted materials drop from three independent mask samplers to one `_MaskMap` sampler. Exact microseconds require Frame Debugger/Profiler proof on target scenes.

## Decision 005: `.asset` Texture2D Output Instead Of PNG Re-Import Loop

Problem: The old mask packer wrote PNG bytes and relied on importer reimport, which is slow and preserves the wrong channel contract.
Solution: Create Texture2D assets directly from `SetPixelData(NativeArray<Color32>)`, write deterministic mips, then request BC7/BC5 compression before asset save.
Rejected Alternatives: `EncodeToPNG`, TGA byte writing, and runtime compression were rejected because they add managed byte arrays, reimport churn, or runtime stalls.
Scalability potential: Low uses smaller max size and the same BC7 path; Middle/High/Ultra increase source resolution or generated normal use without adding runtime sampler count.
Hardware Impact: Runtime receives compressed immutable assets. Editor conversion cost remains cold; exact runtime gain comes from reduced sampler/bandwidth pressure.

## Decision 006: Scanner Before Material Mutation

Problem: Loose `_MetallicGlossMap`, `_OcclusionMap`, and roughness maps may exist across first-party materials, but blindly clearing material slots can break unconverted assets.
Solution: Extend `HectonMaterialChannelPackValidator` to report loose sampler stacks, packed-mask import violations, BC7/linear failures, and netcode exclusion metadata to JSON.
Rejected Alternatives: Project-wide material mutation and shader-specific guesses were rejected because they can destroy authoring data without a generated ARM replacement.
Scalability potential: Low devices benefit first by removing loose samplers from high-frequency materials; higher tiers use the same scan report to decide where richer materials can keep detail elsewhere.
Hardware Impact: Scanner itself is editor-only. Remediation target is two sampler reads saved per converted material; exact microseconds are pending GPU profiler capture.

## Decision 007: Netcode Exclusion Fence

Problem: Generated texture bytes, importer settings, and material assignment changes are visual asset state, not rollback simulation state.
Solution: Document and report that ARM outputs are excluded from rollback snapshots, Merkle state hashes, and `StateRingBuffer`; gameplay may hash stable asset IDs only.
Rejected Alternatives: Hashing texture pixels or importer settings was rejected because it bloats deterministic state and couples rendering imports to gameplay rollback.
Scalability potential: All device tiers consume the same stable asset identity while texture resolution/compression can scale outside simulation.
Hardware Impact: Avoids state payload growth and prevents render-asset changes from increasing rollback memory or hash time. Exact microseconds not applicable without netcode capture.

## Decision 008: Compile Gate Obedience

Problem: Project rules forbid `dotnet build` while CPU is above 50% or another compiler is running.
Solution: Checked `dotnet/csc` process state and CPU before compile; `dotnet/csc` absent but CPU sampled at 100%, so build was not launched.
Rejected Alternatives: Running the compiler anyway or fabricating build success were rejected because both violate the local build gate.
Scalability potential: No runtime effect.
Hardware Impact: No compile proof yet. Static scans and diff checks are the current evidence until CPU gate opens.

## Decision 009: Pointer Alias Hardening

Problem: NativeArray fields without `[NoAlias]` make Burst assume memory overlap and block vectorization; the pack job also lost the prompt-required raw pointer mutation path during earlier safety cleanup.
Solution: Add `[NoAlias]` to non-overlapping NativeArray fields, convert `PackArmTextureJob` to raw `Color32*` lanes with `[NativeDisableUnsafePtrRestriction]`, and write output through `UnsafeUtility.AsRef`.
Rejected Alternatives: Keeping safe NativeArray indexing only, or fetching unsafe pointers inside every `Execute`, were rejected because they either miss the prompt contract or waste per-element instruction budget.
Scalability potential: Same job scales across all quality profiles; low quality reduces authored resolution/bake complexity while high/ultra can spend offline cycles on stronger masks without runtime sampler cost.
Hardware Impact: Improves Burst alias analysis for x86/ARM64 editor machines. Runtime impact remains 0 us because the packer is Editor-only.

## Decision 010: Intrinsics Without Architecture Lock-In

Problem: Task 06 explicitly requires `Unity.Burst.Intrinsics`, but hard-coding x86-only `Sse2` paths risks ARM64 editor incompatibility.
Solution: Use `Unity.Burst.Intrinsics.v128` as the channel-lane packing primitive, writing bytes `0..3` and returning `UInt0`.
Rejected Alternatives: x86 `Sse2`-only pack path and scalar-only pack path were rejected. The former is platform-biased; the latter fails the assignment.
Scalability potential: Identical ARM channel format across Quest/Mac/PC; no hardware binary switch.
Hardware Impact: Keeps packing ABI stable while leaving final instruction selection to Burst per target CPU.

## Decision 011: `.asset` Scanner Compatibility

Problem: The scanner treated any packed mask without `TextureImporter` as invalid, but the packer intentionally outputs `.asset` Texture2D files.
Solution: Add a `.asset` validation path that accepts `Texture2D` assets, checks BC7, POT dimensions, mip chain, and channel variance; imported images still use `TextureImporter` validation.
Rejected Alternatives: Forcing PNG/TGA output just to satisfy importer validation, or ignoring `.asset` masks, were rejected because they undermine the direct `SetPixelData` asset route.
Scalability potential: Low/Middle/High/Ultra all consume the same immutable ARM asset contract.
Hardware Impact: Prevents false scanner violations that would block adoption of the sampler-saving path.

## Decision 012: Prefab Material Surface Included

Problem: Material assets alone do not cover prefab renderer slots, so loose sampler stacks embedded through prefab references could evade the report.
Solution: Extend the validator with an Editor-only prefab renderer material scan and synthetic prefab material paths.
Rejected Alternatives: Asset-only scanning and blind prefab mutation were rejected because the first misses violations and the second risks unrelated authoring damage.
Scalability potential: Wider enforcement means more candidate materials can drop from three mask samplers to one ARM sampler.
Hardware Impact: Editor-only scan cost; runtime gains depend on remediated material count.

## Decision 013: Editor Blackbox Ring For Texture Packing

Problem: The packer had JSON reports, but no fixed forensic ring satisfying the local blackbox rule for the last 300 critical states.
Solution: Add `TexturePackerTelemetryEntry` as an explicit 64-byte DTO and an Editor-only `TexturePackerBlackBox` with a 300-entry `NativeArray` ring, lifecycle disposal on assembly reload/quit, manual dump menu, and automatic dump on pack exception or non-finite timing.
Rejected Alternatives: Chat-only status, managed `List<>` telemetry, and runtime StateRingBuffer participation were rejected. Chat is not evidence, `List<>` is GC-backed, and generated texture state is visual asset data excluded from rollback authority.
Scalability potential: Low/Middle/High/Ultra runtime paths remain untouched; editor failures now leave deterministic forensic bytes while the shipped game still pays 0 us.
Hardware Impact: 300 * 64 = 19200 bytes of Editor-only persistent forensic memory. Runtime impact is 0 us and 0 bytes; low-end silicon gains remain from sampler reduction, not from this diagnostic path.

## Decision 014: Struct Request Mutation Fix

Problem: `TexturePackerRequest` was intentionally converted from a class to a struct, but `ValidateRequest` still accepted it by value. Default `OutputName`, `OutputFolder`, and `MaxSize` writes were therefore lost, risking invalid root-level asset paths for fallback callers.
Solution: Change validation to `ValidateRequest(ref TexturePackerRequest request)`, keep the public pack API value-based for caller isolation, and normalize request defaults before any path or dimension resolution.
Rejected Alternatives: Reverting the request to a reference type, duplicating default logic in every caller, or assuming the UI always supplies all fields were rejected. Reference semantics reintroduce avoidable mutable object state; duplicated defaults drift; assuming a single caller breaks the legacy selection menu.
Scalability potential: All quality profiles now reach the same stable output root. Non-square production scans keep enough resolution because each axis is resolved from source texture dimensions before max-size clamp.
Hardware Impact: Runtime remains 0 us. Editor prevents accidental undersized output and invalid asset path churn; exact bake-time delta is negligible compared with texture readback/compression.

## Decision 015: Continuous Macro Octave Quality Curve

Problem: Macro-noise strength used continuous `GlobalQualityWeight`, but the FBM octave mix itself stayed fixed. That was visually acceptable but did not prove mathematical quality collapse below q=0.3.
Solution: Feed `GlobalQualityWeight` into the FBM function. Base low-frequency noise always remains, while octave 1 and octave 2 weights fade through `math.smoothstep(0.18,0.70,q)` and `math.smoothstep(0.48,1.0,q)`, with normalized total weight and polynomial strength shaping.
Rejected Alternatives: Authoring separate low/high textures, runtime shader macro-noise, or hard `if (q < 0.3)` branches were rejected. Separate outputs drift, runtime noise violates sampler/ALU goals, and hard thresholds create tier pops.
Scalability potential: Low receives one broad macro field; Middle gradually admits the second octave; High/Ultra restores the full three-octave variation while still baking it offline into the same ARM mask.
Hardware Impact: Runtime remains 0 us and one ARM sampler. Editor bake ALU changes are cold; the saved runtime macro sampler remains the actual low-end silicon win.

## Decision 016: CSV Flags Must Actually Control Work

Problem: The CSV parser defaulted every parsed profile to macro noise, Toksvig mips, and Sobel normals before reading the flag column. This meant a prop profile could not disable macro or normal generation, making the human tuning bridge mostly decorative.
Solution: Parse the flag column as lowercase FNV-1a tokens directly from the byte stream. Empty flags keep the default recipe; explicit `none`, `off`, `false`, or `0` returns zero flags; `macro/noise`, `toksvig/mip`, `normal/sobel`, and `invert/smoothness` opt into the exact stages requested.
Rejected Alternatives: Managed `Split`, enum reflection, or UI-only toggles were rejected. They either allocate, add fragile reflection, or prevent profile files from being authoritative.
Scalability potential: Low/prop profiles can skip Sobel and macro bake completely; terrain profiles can enable full macro/Toksvig behavior; high/ultra assets can opt into all offline detail without changing runtime shader sampler count.
Hardware Impact: Runtime remains 0 us. Editor batch time drops for profiles that disable Sobel/macro stages; exact milliseconds require Unity menu-run proof.

## Decision 017: Forge Must Not Override CSV Macro Authority

Problem: The byte parser could return zero flags for `none/off/0`, but `TextureChannelPackerWindow.BuildRequest` still initialized every request with `FlagInjectMacroNoise`. That made the parser correct and the UI facade wrong.
Solution: Initialize request flags from `profile.Flags`, then apply only the visible user toggles for invert roughness, Toksvig mips, and Sobel normals. Macro remains controlled by the profile data and can be further neutralized by a zero macro strength.
Rejected Alternatives: Adding another macro checkbox, leaving macro always on, or moving all flag control to UI state were rejected. Another checkbox duplicates profile authority; always-on violates prop profiles; UI-only state makes CSV recipes decorative.
Scalability potential: Low/prop profiles can skip macro and normal jobs entirely; terrain profiles keep macro/Toksvig; high/ultra can enable all offline stages from data without changing runtime sampler count.
Hardware Impact: Runtime remains 0 us. Editor batch work now follows the selected profile; disabled macro avoids one full-pixel pass for prop batches. Exact milliseconds require Unity menu-run proof.

## Decision 018: Remove Last DTO-Style Properties From Owned Editor Surface

Problem: `PackedMaskAnalysis` in the validator still used get-only properties. It was Editor-only and not a Burst pixel DTO, but it weakened the CS1612/property-eradication proof for this domain.
Solution: Replace the properties with raw readonly fields while keeping the struct immutable at construction.
Rejected Alternatives: Keeping properties because they were cold-path, or converting the validator to a class, were rejected. The first leaves an avoidable audit scar; the second adds reference semantics without benefit.
Scalability potential: No visual tier behavior changes. It keeps the domain's data surface closer to the raw-field rule used by the real Burst DTOs.
Hardware Impact: Runtime remains 0 us. Editor impact is negligible; this is primarily compile-surface and audit hardening.

## Decision 019: Preserve Non-Square Source Axes

Problem: The request mutation fix prevented width-only collapse, but the packer still resolved output width and height from the same max dimension. A 1024x4096 source could become square, wasting texture memory and potentially altering authored aspect.
Solution: Resolve width and height independently across AO, Roughness, Metallic, and Albedo sources. Each axis is rounded to power-of-two and clamped to the selected max size.
Rejected Alternatives: Always-square output and source-width-only output were rejected. Square output wastes VRAM for trims; width-only output loses vertical detail.
Scalability potential: Low profiles can clamp each axis to 512/1024/2048 without forced square bloat; high/ultra profiles preserve asymmetric source detail when authored.
Hardware Impact: Runtime remains one ARM sampler. Non-square assets avoid unnecessary texels, improving VRAM residency and upload size; exact savings depend on source aspect.

## Decision 020: Batch Queue Must Advance After Per-Set Fault

Problem: `TryPackArmAsset` records a fault and rethrows to preserve forensic visibility. The Forge update loop did not catch that exception, so one bad source set could keep the same `_pendingIndex` and repeatedly fault on editor update.
Solution: Catch exceptions inside `TickPackingQueue`, increment the failed counter, log one editor error with the source key and exception type, then advance `_pendingIndex`.
Rejected Alternatives: Swallowing exceptions inside the packer or stopping the whole batch were rejected. The packer must dump blackbox evidence; the batch should still process independent source sets.
Scalability potential: Large art-library batches degrade by skipping bad sets instead of stalling the entire queue. Low/high visual output rules are unchanged.
Hardware Impact: Runtime remains 0 us. Editor prevents repeated failing work and unbounded Console spam from a single corrupt texture set.

## Decision 021: Isolate Texture Packer From Broad Editor Assembly

Problem: The packer files lived under the pre-existing broad `Hecton8.Editor.asmdef`, which references multiple Hecton8 runtime assemblies. The code itself did not use sibling namespaces, but the assembly evidence did not satisfy the compile-wall audit.
Solution: Move the texture packer files into `Assets/_Project/Scripts/Editor/TextureChannelPacker/` and add `Hecton8.Rendering.TexturePacker.Editor.asmdef` with only Unity Collections, Mathematics, Burst, and Jobs references.
Rejected Alternatives: Editing the broad shared `Hecton8.Editor.asmdef` or leaving the code under that assembly were rejected. The first risks other editor tools owned by other agents; the second leaves a false compile-wall dependency surface.
Scalability potential: No visual tier behavior changes. Iteration speed improves because texture-packer edits compile inside a narrower Editor assembly.
Hardware Impact: Runtime remains 0 us. Developer compile churn is reduced; exact compile seconds require Unity import/build proof after CPU gate opens.

## Decision 022: Accessor Purity Naming Guard

Problem: Global Systems Doctrine requires `Get*`, `TryGet*`, `Resolve*`, and `Read*` accessors to be pure. Several owned Editor helpers used those names while creating folders, building strings, or advancing CSV parser cursors, which weakened static evidence even though the code remained Editor-only.
Solution: Rename impure helpers to command/parser/build names: `CreateUniqueAssetPath`, `BuildSetKey`, `TryParseProfile`, `ParseFixedStringColumn`, `ParseFloatColumn`, `ParseFlagsColumn`, `BuildPrefabMaterialPath`, and `BuildFormatLabel`. Remaining `Resolve*` helpers are pure dimension/mip math. `.Complete()` sites now carry explicit Editor materialization comments.
Rejected Alternatives: Leaving names unchanged with comments, or over-renaming pure math helpers, were rejected. Comments do not satisfy static audits; renaming pure math helpers would add churn without improving doctrine compliance.
Scalability potential: No visual tier behavior changes. The same continuous `GlobalQualityWeight` macro bake and one-sampler ARM contract remain intact across Low/Middle/High/Ultra.
Hardware Impact: Runtime remains 0 us. Editor behavior is unchanged; the gain is doctrine-proofed call semantics and reduced risk of future hot-path misuse.

## Decision 023: Prune Atlas Stale Texture-Packer Paths Without Reopening Broad Editor Assembly

Problem: `BINARY_PAYLOAD_INTEGRATION_LEDGER.md` and `Tools/AtlasCheck.py` exposed stale references to `Assets/_Project/Scripts/Editor/HectonMaskChannelPacker.cs` and `Assets/_Project/Scripts/Editor/HectonMaterialChannelPackValidator.cs` after the packer moved into the isolated `TextureChannelPacker` Editor asmdef. Restoring shim files at the old path would reattach texture-packer code to the broad editor assembly and violate the compile-wall goal.
Solution: Update only the dependency graph/tooling/ledger references owned by the static atlas surface: the old mask hotspot now points to `HectonArmTextureChannelPacker.cs` at the actual RenderTexture capture path, the validator hotspot points to `TextureChannelPacker/HectonMaterialChannelPackValidator.cs`, and the atlas status/test/ledger strings reflect the current `ATLAS_CHECK_FAIL references=6779 missing=60` result.
Rejected Alternatives: Recreating old files, editing broad `Hecton8.Editor.asmdef`, or rewriting all DOC_GLOBAL historical reports were rejected. Shims damage assembly isolation; broad asmdef edits touch other agents; historical report rewrites exceed SHINOBU_214 authority.
Scalability potential: No visual tier behavior changes. The one-sampler ARM contract and continuous macro bake remain intact while documentation now points at the narrower Editor assembly.
Hardware Impact: Runtime remains 0 us. Developer compile wall remains protected because no old-path source shim was introduced; static atlas noise from SHINOBU_214 dropped by two missing references.
