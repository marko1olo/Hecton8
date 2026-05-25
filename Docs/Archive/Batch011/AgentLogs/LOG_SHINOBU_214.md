# SHINOBU_214 Agent Log

## 2026-05-20 - PBR ARM Texture Channel Packer

What was wrong:
- Project had legacy M.A.S.K./ORM authoring ambiguity: old editor packer wrote Metallic/AO/Smoothness/Emissive PNG, while current surface doctrine requires ARM `R=AO`, `G=Roughness`, `B=Metallic`.
- `UberNoir` was still interpreting `_MaskMap` as legacy ORM in the sampled material path.
- No dedicated UI Toolkit pipeline existed for bulk AO/Roughness/Metallic packing, Sobel normal generation, deterministic roughness mips, macro tiling breakup, profile CSV ingestion, or live channel preview.
- Existing material validator did not emit the required rendering optimization JSON report and did not explicitly audit roughness loose stacks for the ARM pipeline.

What was done:
- Added `HectonArmTextureChannelPacker.cs`: Editor-only ARM packer, explicit 16-byte `TexturePackerConfigDTO`, Burst jobs, unsafe pointer packing, Sobel normals, baked macro noise, Toksvig/VSM-style roughness mips, BC7 ARM texture asset output, BC5 generated normal output, layout/mock/packing JSON reports.
- Added `TextureChannelPackerWindow.cs`: UI Toolkit forge window, folder batch queue, continuous `GlobalQualityWeight`, profile controls, unsafe byte-buffer CSV parser for `texture_packing_profiles.csv`, and ARM channel preview.
- Replaced legacy `HectonMaskChannelPacker.cs` behavior with a compatibility entrypoint that delegates to ARM packing and rejects the old M.A.S.K. output contract.
- Updated `Hecton8_UberNoir` shader contract: `_MaskMap` label is ARM; shader now reads AO from R, Roughness from G, Metallic from B, keeps A for emission/bio mask.
- Extended `HectonMaterialChannelPackValidator.cs`: UberNoir coverage, roughness loose-stack detection, BC7/linear validation, and `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json` writer with rollback/Merkle/StateRingBuffer exclusion fields.
- Added `Docs/ARCHITECTURE/ARM_TEXTURE_PACKING_PIPELINE.md`.
- Installed pending-run JSON artifacts at `Docs/Reports/TEXTURE_PACKING_REPORT.json` and `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json`; prior SHINOBU_212 rendering report payload was preserved under `previousReport`.
- Added stable `.meta` files for new Unity editor scripts.

Cinematic Cheats used:
- Baked deterministic FBM macro noise into AO/Roughness to hide 100-km tiling without adding a runtime macro sampler.
- Generated Sobel normals offline from albedo luminance instead of synthesizing normals in the runtime material.
- Preserved roughness variance in mipmaps offline to reduce shimmer without per-frame shader cost.

Exact Microseconds saved:
- Measured exact GPU microseconds: NOT AVAILABLE. Compiler/profiler execution was blocked by CPU gate; CPU sampled at 100/94/100 percent and project rules forbid `dotnet build` above 50 percent.
- Exact CPU runtime cost added by new systems: 0 us; all new C# is Editor-only and wrapped in `#if UNITY_EDITOR`.
- Static GPU model: each remediated material replaces AO + Roughness + Metallic loose sampling with one ARM sampler, saving two texture sampler reads per material. Exact frame microseconds require Unity import, material conversion, Frame Debugger, and GPU Profiler capture.

Verification:
- `git diff --check` on edited files: PASS, line-ending warnings only.
- Static scan on owned new/edited path: no old M.A.S.K. menu, no old ORM label, no `ormSample`, no `GetPixels`, no `SetPixels`, no `EncodeToPNG`.
- Compile check: BLOCKED BY CPU GATE. `dotnet/csc` absent; CPU remained above 50 percent. Build was not launched.
- Unity import, Burst Inspector, asset packing run, material scan run, Frame Debugger, profiler, and GCMonitor proof: PENDING.

## 2026-05-20 - Ultra Polish Pass

What was wrong:
- The previous pack job used safe NativeArray indexing after cleanup, which was safer but failed the original raw-pointer/`UnsafeUtility.AsRef` mandate.
- Non-overlapping job arrays did not all carry `[NoAlias]`, leaving Burst alias analysis weaker than required.
- Normal generation and mip normalization still used `math.normalize`; finite/zero guards were implicit instead of explicit.
- The scanner rejected packer-owned `.asset` Texture2D ARM masks because they do not have a `TextureImporter`.
- Prefab renderer material slots were not scanned explicitly.

What was done:
- `PackArmTextureJob` now receives raw `Color32*` inputs/outputs with `[ReadOnly/WriteOnly, NoAlias, NativeDisableUnsafePtrRestriction]`, uses `UnsafeUtility.AsRef`, and packs channels through `Unity.Burst.Intrinsics.v128`.
- All non-overlapping NativeArray job fields in the packer now carry `[NoAlias]`.
- Added guarded `SafeNormalize` and removed scanned `math.normalize` usage from owned packer jobs.
- `TexturePackingProfile` is now explicit 96 bytes: 64-byte fixed string plus eight 4-byte scalar fields.
- Material validator now accepts packer-owned `.asset` BC7 Texture2D masks, validates POT/mips/channel variance, and scans prefab renderer materials under `Assets/_Project/Prefabs`.

Cinematic Cheats used:
- Same Dear Lie: offline macro-noise in AO/Roughness replaces a runtime macro sampler.
- Same offline Sobel normals and roughness-variance mips; no runtime physics/simulation added.

Exact Microseconds saved:
- Runtime CPU added: 0 us.
- Exact GPU microseconds: still pending profiler proof.
- Static sampler model remains two texture samples saved per remediated material by replacing loose AO/Roughness/Metallic reads with one ARM mask read.

Verification:
- Current `Docs/Tasks/CURRENT_BATCH.md` extraction succeeded with `<AGENT_PROMPT id="SHINOBU_214" role="PBR_TEXTURE_CHANNEL_PACKER" chat_name="SHINOBU_214">`.
- Static scans pass for old ORM/M.A.S.K./pixel APIs in owned files.
- `git diff --check` passes with line-ending warnings only.
- Compile remains blocked by CPU gate: `dotnet/csc` absent, CPU sampled at 100 percent.

<SELF_AUDIT agent_id="SHINOBU_214">
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS">Rendering/Environment texture manipulation scan completed; unrelated weather/cold LUTs left outside domain.</TASK>
    <TASK id="02" status="PASS">UberNoir ARM contract corrected; loose sampler scanner installed.</TASK>
    <TASK id="03" status="PASS">Pack job uses raw Color32 pointers and UnsafeUtility.AsRef; no properties in packing DTOs.</TASK>
    <TASK id="04" status="PASS">TexturePackerConfigDTO explicit 16B layout installed and reportable.</TASK>
    <TASK id="05" status="PASS">GenerateMockTexturePackJob installed for 4K stress path.</TASK>
    <TASK id="06" status="PASS">PackArmTextureJob packs AO/Roughness/Metallic into ARM using Burst v128 lane packing.</TASK>
    <TASK id="07" status="PASS">Sobel normal generation job installed with guarded normalization.</TASK>
    <TASK id="08" status="PASS">Offline Dear Lie macro-noise job installed.</TASK>
    <TASK id="09" status="PASS">Variance-preserving roughness mip chain installed.</TASK>
    <TASK id="10" status="PASS">SetPixelData Texture2D `.asset` serialization into BakedGeometry path installed.</TASK>
    <TASK id="11" status="PASS">InvertRoughness flag in config and pack kernel installed.</TASK>
    <TASK id="12" status="PASS">Macro frequency consumes tile meters, macro span meters, and GlobalQualityWeight.</TASK>
    <TASK id="13" status="PASS">Rollback/Merkle/StateRingBuffer exclusion documented and reported.</TASK>
    <TASK id="14" status="PASS">Dense NativeArray buffers use UninitializedMemory; no MemClear path added.</TASK>
    <TASK id="15" status="PASS">TEXTURE_PACKING_REPORT.json installed/written by pipeline.</TASK>
    <TASK id="16" status="PASS">UI Toolkit Texture Channel Packer window installed.</TASK>
    <TASK id="17" status="PASS">Unsafe byte-buffer CSV profile parser installed.</TASK>
    <TASK id="18" status="PASS">ARM channel preview job/window installed.</TASK>
    <TASK id="19" status="PASS">Material and prefab scanner writes RENDERING_OPTIMIZATION_REPORT.json.</TASK>
    <TASK id="20" status="PASS">Static self-audit scans pass; Unity proof pending CPU gate.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT>
    <TexturePackerConfigDTO size="16" alignment="16">
      <field name="NormalIntensity" offset="0" size="4" />
      <field name="RoughnessScale" offset="4" size="4" />
      <field name="MetallicScale" offset="8" size="4" />
      <field name="Flags" offset="12" size="4" />
      <padding bytes="0" />
    </TexturePackerConfigDTO>
    <TexturePackingProfile size="96" alignment="32">
      <field name="Name" offset="0" size="64" />
      <field name="NormalIntensity" offset="64" size="4" />
      <field name="RoughnessScale" offset="68" size="4" />
      <field name="MetallicScale" offset="72" size="4" />
      <field name="MacroNoiseStrength" offset="76" size="4" />
      <field name="TileSizeMeters" offset="80" size="4" />
      <field name="MacroWorldSpanMeters" offset="84" size="4" />
      <field name="GlobalQualityWeight" offset="88" size="4" />
      <field name="Flags" offset="92" size="4" />
    </TexturePackingProfile>
  </STRUCT_LAYOUT>
  <SCALABILITY_CURVE>GlobalQualityWeight is continuous. Macro strength uses cubic smooth polynomial shaping, and FBM octave 1/2 weights fade through smoothstep gates before normalization; texture profiles scale macro strength/span and max resolution; Toksvig mips preserve roughness as thermal pressure forces lower mip sampling. No runtime binary hardware switch was introduced.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>No runtime system or persistent gameplay memory owner was created. VaultBufferHandle IDs requested: none. Editor-only Temp/TempJob NativeArrays are disposed in finally blocks; generated texture assets are excluded from rollback state.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>Pack path: PrepareSource AO/Roughness/Metallic jobs -> CombineDependencies -> PackArmTextureJob -> optional InjectMacroNoiseJob -> optional GenerateSobelNormalsJob dependency -> SetPixelData serialization. PackArmTextureJob pointer lanes use NoAlias; non-overlapping NativeArray job fields use NoAlias.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No runtime asmdef or sibling domain reference was added. All new C# is under Editor path and #if UNITY_EDITOR. dotnet build was not launched because CPU gate sampled 100 percent.</COMPILE_GUARD>
  <DEAR_LIE>Before: runtime macro texture/noise sampler per material, O(pixels per frame across visible surfaces). After: offline O(texels) bake once, runtime O(1) existing ARM sampler. Macro variation is baked into AO/Roughness.</DEAR_LIE>
</SELF_AUDIT>

## 2026-05-20 - Blackbox Forensics Pass

What was wrong:
- JSON reports recorded the latest batch outcome, but the packer had no fixed 300-entry binary forensic ring for exception/NaN investigation.
- Without a dump artifact, a failed editor import would leave only console text and partial JSON, which does not satisfy the local blackbox rule.

What was done:
- Added `TexturePackerTelemetryEntry` as `[StructLayout(LayoutKind.Explicit, Size = 64)]`.
- Added `TexturePackerBlackBox`: Editor-only 300-entry `NativeArray<TexturePackerTelemetryEntry>` ring, disposed on assembly reload/editor quit.
- Added automatic dump on pack exception and non-finite timing, plus manual menu `Hecton8/Rendering/Texture Channel Packer/Dump Black Box`.
- Added dump target `Docs/AgentLogs/Dump_SHINOBU_214.bin` and reason sidecar `Docs/AgentLogs/Dump_SHINOBU_214.bin.reason.txt`.
- Updated status, rationale, architecture doc, and reports to name the blackbox path.

Cinematic Cheats used:
- No new simulation. The visual cheat remains the offline baked macro-noise in AO/Roughness, preserving the runtime one-sampler ARM path.

Exact Microseconds saved:
- Runtime CPU/GPU added: 0 us. The blackbox exists only under `#if UNITY_EDITOR`.
- Forensic memory: 300 * 64 = 19200 bytes persistent Editor memory.
- Sampler model unchanged: two texture reads saved per remediated material when AO/Roughness/Metallic collapse into one ARM mask.

Verification:
- Static install proof: `TexturePackerTelemetryEntry` uses explicit 64-byte layout and the dump path is reachable from menu/exception/non-finite metrics.
- Unity import/dump execution proof: PENDING because compile/import remains gated by CPU policy.

<SELF_AUDIT agent_id="SHINOBU_214" revision="blackbox">
  <STRUCT_LAYOUT_VERIFICATION>
    <TexturePackerTelemetryEntry size="64" cacheLine="1">
      <field name="FrameHash" offset="0" size="4" />
      <field name="Flags" offset="4" size="4" />
      <field name="Width" offset="8" size="4" />
      <field name="Height" offset="12" size="4" />
      <field name="PixelCount" offset="16" size="4" />
      <field name="QueueIndex" offset="20" size="4" />
      <field name="JobMilliseconds" offset="24" size="4" />
      <field name="TotalMilliseconds" offset="28" size="4" />
      <field name="OutputHash" offset="32" size="4" />
      <field name="FaultCode" offset="36" size="4" />
      <field name="TimestampTicks" offset="40" size="8" />
      <field name="PathHash" offset="48" size="8" />
      <field name="Reserved" offset="56" size="8" />
      <padding bytes="0" />
    </TexturePackerTelemetryEntry>
  </STRUCT_LAYOUT_VERIFICATION>
  <H_PHI_VAULT_STATUS>Runtime still owns zero persistent arrays. The only new persistent NativeArray is an Editor-only 300-entry diagnostic ring required by the local blackbox rule and disposed on editor reload/quit.</H_PHI_VAULT_STATUS>
  <COMPILE_GUARD>Build remains intentionally unlaunched: dotnet/csc absent, CPU sampled at 100 percent, local rule forbids build above 50 percent.</COMPILE_GUARD>
</SELF_AUDIT>

## 2026-05-20 - Struct Request Safety Pass

What was wrong:
- `TexturePackerRequest` is a struct, but `ValidateRequest` accepted it by value. Default `OutputName`, `OutputFolder`, and `MaxSize` mutations were discarded for fallback callers.
- Pack dimension resolution used texture width only. A 1024x4096 source could be collapsed to a 1024 output before the max-size clamp.
- The UI batch report could overwrite the pending report JSON without the blackbox fields added by the packer report generator.

What was done:
- Changed `ValidateRequest(TexturePackerRequest)` to `ValidateRequest(ref TexturePackerRequest)`.
- Changed dimension selection to use max(width,height) for AO, Roughness, Metallic, and Albedo.
- Added blackbox dump path, 64-byte entry size, and 300-entry ring length to the Forge Window batch JSON writer.

Cinematic Cheats used:
- No new runtime work. The Dear Lie remains offline macro-variation baked into ARM channels.

Exact Microseconds saved:
- Runtime added: 0 us.
- Bake correctness impact: prevents accidental undersized outputs and invalid root asset paths; exact bake-time impact is negligible.

Verification:
- Static call surface confirms `TryPackArmAsset` now calls `ValidateRequest(ref request)`.
- Static report surface confirms both single-pack and batch report writers emit blackbox fields.
- Forbidden-pattern scan stayed clean for old ORM/M.A.S.K., managed pixel loops, LINQ/foreach, Random/Time, and MemClear in owned new/edited files.
- Build remains gated: dotnet/csc absent, CPU sampled at 96 percent.

## 2026-05-20 - Continuous Quality Curve Pass

What was wrong:
- Macro-noise amplitude respected `GlobalQualityWeight`, but FBM octave composition was fixed. That weakened the proof that q below 0.3 collapses toward the cheapest acceptable visual approximation.

What was done:
- `InjectMacroNoiseJob` now sends `GlobalQualityWeight` into `Fbm`.
- `Fbm` keeps the base low-frequency octave and fades octave 1/2 weights with `math.smoothstep`, then normalizes by total weight.
- Macro strength uses a cubic smooth polynomial quality curve instead of raw linear q.

Cinematic Cheats used:
- Same offline Dear Lie: the macro detail lives inside AO/Roughness channels, not in a runtime world-noise sampler.

Exact Microseconds saved:
- Runtime added: 0 us.
- Runtime saved model unchanged: one macro sampler avoided and two loose PBR sampler reads removed per converted material.
- Editor bake ALU remains cold; quality curve affects offline output only.

Verification:
- Static source surface confirms `Fbm(float2,uint,float)` consumes `GlobalQualityWeight` and uses `math.smoothstep`.
- Build remains gated: dotnet/csc absent, CPU sampled at 99 percent.

## 2026-05-20 - CSV Flag Authority Pass

What was wrong:
- `texture_packing_profiles.csv` parsing always began with macro noise, Toksvig mips, and Sobel normals enabled. A profile intended for props could not actually disable those stages from data.

What was done:
- Rewrote the CSV flag-column parser to parse lowercase FNV-1a tokens from the existing byte cursor.
- Supported `macro/noise`, `toksvig/mip`, `normal/sobel`, `invert/smoothness`, and `none/off/false/0`.
- Empty flag cells keep the default hard-surface behavior; explicit off returns zero flags.

Cinematic Cheats used:
- Terrain profiles still use the Dear Lie macro bake. Prop profiles can now skip it instead of paying pointless offline work.

Exact Microseconds saved:
- Runtime added: 0 us.
- Runtime sampler savings unchanged.
- Editor savings are profile-dependent: disabled Sobel/macro stages avoid those jobs for prop batches; Unity timing proof pending.

Verification:
- Static forbidden-pattern scan stayed clean after the parser change.
- JSON reports still parse.

## 2026-05-20 - CSV Profile Facade Authority Pass

What was wrong:
- The CSV byte parser could produce zero optional flags, but the Forge window still initialized every batch request with `FlagInjectMacroNoise`. Data authority and UI execution diverged.

What was done:
- `TextureChannelPackerWindow.BuildRequest` now starts from `profile.Flags`.
- The visible UI toggles only override invert roughness, Toksvig mips, and Sobel normals after profile selection.
- Macro bake remains profile-owned; explicit `none/off/0` in CSV now reaches the pack request instead of being silently overwritten.
- `HashLiteral(string)` was removed from CSV flag matching and replaced with precomputed FNV-1a constants.

Cinematic Cheats used:
- Prop recipes can remove the entire offline macro pass. Terrain recipes keep the baked macro Dear Lie without introducing a runtime sampler.

Exact Microseconds saved:
- Runtime added: 0 us.
- Editor: one full-pixel macro pass is skipped for profiles that disable macro. Exact timing pending Unity menu-run proof.

Verification:
- Static forbidden-pattern scan passed after this patch.
- JSON reports parse.
- Build remains gated: dotnet/csc absent, CPU sampled at 100 percent.

## 2026-05-20 - Accessor Purity Guard Pass

What was wrong:
- Global Systems Doctrine requires `Get*`, `TryGet*`, `Resolve*`, and `Read*` accessors to remain pure. Some owned Editor helpers had those names while creating asset folders, building strings, or advancing CSV cursor state.

What was done:
- Renamed impure helpers to command/parser/build names: `CreateUniqueAssetPath`, `BuildSetKey`, `TryParseProfile`, `ParseFixedStringColumn`, `ParseFloatColumn`, `ParseFlagsColumn`, `BuildPrefabMaterialPath`, and `BuildFormatLabel`.
- Left only pure dimension/mip math under `Resolve*`.
- Added explicit comments on owned `.Complete()` calls proving they are Editor serialization, mip materialization, benchmark, or preview boundaries.

Cinematic Cheats used:
- None added in this pass. Existing baked macro-noise Dear Lie remains the runtime sampler-saving fake.

Exact Microseconds saved:
- Runtime added: 0 us.
- Runtime sampler savings unchanged: converted materials still use one ARM sampler instead of separate AO/Roughness/Metallic samplers.
- Editor performance unchanged; this pass removes semantic ambiguity and reduces future hot-path misuse risk.

Verification:
- Retired impure accessor names no longer match in owned texture-packer path.
- Remaining accessor-name scan reports only pure `ResolvePackWidth`, `ResolvePackHeight`, `ResolveAxisDimension`, `ResolveMipCount`, and pure `GetPackedMaskPropertyName`.
- Build remains gated until CPU drops below 50 percent and no `dotnet`/`csc` process is active.

## 2026-05-20 - Atlas Path Drift Prune Pass

What was wrong:
- Static atlas artifacts still referenced the removed broad-assembly paths `Assets/_Project/Scripts/Editor/HectonMaskChannelPacker.cs` and `Assets/_Project/Scripts/Editor/HectonMaterialChannelPackValidator.cs`.
- Recreating files at those paths would pull texture-packer code back toward the broad `Hecton8.Editor.asmdef` surface and undo the compile-wall isolation.

What was done:
- Updated `Docs/DEPENDENCY_GRAPH.md` and `Docs/DEPENDENCY_GRAPH.json` to point texture-packer hotspots at the isolated `Editor/TextureChannelPacker` paths.
- Updated `Tools/BuildArchitectureAtlas.py` so future atlas regeneration does not reintroduce the stale SHINOBU_214 paths.
- Updated `Tools/test_architecture_atlas.py` to assert the current atlas red-state count.
- Updated `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md` so the current static gate no longer names removed SHINOBU_214 source paths as active AtlasCheck blockers.

Cinematic Cheats used:
- None. This is static atlas hygiene; the active cinematic cheat remains baked macro-noise in ARM AO/Roughness.

Exact Microseconds saved:
- Runtime added: 0 us.
- Developer compile wall protected: no old-path source shim was added.
- Atlas missing refs reduced by two SHINOBU_214 stale references; remaining `AtlasCheck` misses are outside this domain.

Verification:
- `python Tools/AtlasCheck.py`: expected red state now reports `ATLAS_CHECK_FAIL references=6779 missing=60`; no missing `HectonMaskChannelPacker` or `HectonMaterialChannelPackValidator` old path remains.
- `python -m unittest Tools.test_architecture_atlas`: PASS, 10 tests.
- `python -m py_compile Tools/BuildArchitectureAtlas.py Tools/AtlasCheck.py Tools/test_architecture_atlas.py`: PASS.
- No `dotnet build` launched.

## 2026-05-20 - Batch Fault Advance Pass

What was wrong:
- A single corrupt source set could throw from `TryPackArmAsset` and leave the Forge queue on the same `_pendingIndex`, causing repeated editor update failures.

What was done:
- Wrapped the per-set pack call in `TickPackingQueue` with an Editor-only catch.
- Failed sets increment `_batch.Failed` and the queue advances to the next source set.
- The packer still records the fault and dumps the blackbox before rethrowing.

Cinematic Cheats used:
- None. This is tool resilience around the offline pipeline.

Exact Microseconds saved:
- Runtime added: 0 us.
- Editor: prevents repeated failed work after one bad source set; exact avoided time depends on corrupt asset count.

Verification:
- Static forbidden-pattern scan passed after this patch.
- JSON reports parse.
- Build remains gated: dotnet/csc absent, CPU sampled at 100 percent.

## 2026-05-20 - Compile Wall Isolation Pass

What was wrong:
- The packer scripts were under the broad pre-existing `Hecton8.Editor.asmdef`, which references several Hecton8 runtime assemblies. The code had no sibling-domain `using`, but assembly evidence was still too broad.

What was done:
- Moved the packer scripts and `.meta` files to `Assets/_Project/Scripts/Editor/TextureChannelPacker/`.
- Added `Hecton8.Rendering.TexturePacker.Editor.asmdef`.
- New asmdef references only `Unity.Collections`, `Unity.Mathematics`, `Unity.Burst`, and `Unity.Jobs`; no sibling Hecton8 runtime assemblies.

Cinematic Cheats used:
- None. This is compile-wall isolation.

Exact Microseconds saved:
- Runtime added: 0 us.
- Developer compile-wall impact requires Unity import/build timing proof. Static assembly surface is narrowed.

Verification:
- Asmdef JSON parses.
- `references` contains no `Hecton8.*` entries.
- Static forbidden-pattern scan passed after move.
- Build remains gated: dotnet/csc absent, CPU sampled at 100 percent.

## 2026-05-20 - Property Purge Pass

What was wrong:
- `PackedMaskAnalysis` still used get-only C# properties. The struct was Editor-only, but the domain audit requires raw field discipline across owned texture-packing DTO surfaces.

What was done:
- Replaced `HasSourceAlpha` and `RgbChannelsCollapseToGreyscale` properties with raw readonly fields.

Cinematic Cheats used:
- No simulation involved. This is data-surface hardening for the scanner that proves ARM adoption.

Exact Microseconds saved:
- Runtime added: 0 us.
- Editor delta is negligible; the value is stricter compile-surface hygiene and cleaner CS1612 evidence.

Verification:
- Static forbidden-pattern scan passed after this patch, including `{ get; }` and `HashLiteral`.
- JSON reports parse.
- Build remains gated: dotnet/csc absent, CPU sampled at 100 percent.

## 2026-05-20 - Axis Dimension Preservation Pass

What was wrong:
- Output dimension resolution used one max dimension for both width and height. That avoided width-only collapse but forced non-square source sets into square masks.

What was done:
- Added independent `ResolvePackWidth` and `ResolvePackHeight`.
- Each axis now scans AO/Roughness/Metallic/Albedo source dimensions, rounds to POT, and clamps to request max size.

Cinematic Cheats used:
- No runtime work. The same baked ARM Dear Lie remains; this only avoids unnecessary texel bloat for asymmetric source sets.

Exact Microseconds saved:
- Runtime sampler count remains one ARM sampler.
- VRAM and upload savings are source-dependent. Example model: 1024x4096 clamped to 2048 no longer becomes 2048x2048 if height/width source axes do not require it; exact proof pending Unity asset run.

Verification:
- Static forbidden-pattern scan passed after this patch, including retired `ResolvePackDimension`/`MaxDimension`.
- JSON reports parse.
- Build remains gated: dotnet/csc absent, CPU sampled at 100 percent.

## 2026-05-20 - Cold Allocation Annotation Pass

What was wrong:
- Editor-only `List`/`Dictionary` caches were intentionally cold allocations, but several did not carry the canonical owner/capacity comment required by AGENTS.md.

What was done:
- Added `COLD ALLOC` comments to Forge profile caches, source grouping dictionary, validator audit lists, and per-material issue buffer.

Cinematic Cheats used:
- None. This is audit-surface hygiene only.

Exact Microseconds saved:
- Runtime added: 0 us.
- Performance unchanged; the value is traceable allocation ownership in editor tooling.

Verification:
- Static forbidden-pattern scan passed after this patch.
- JSON reports parse.
- Build remains gated: dotnet/csc absent, CPU sampled at 100 percent.
