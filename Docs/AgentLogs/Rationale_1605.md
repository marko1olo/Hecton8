# Rationale 1605 - SUBSTANCE_SHADER_AND_TEXTURE_BAKER

## Decision 84 - Compute Kernel Lookup Must Be A Controlled Failure

Problem: `TryDispatchAndWrite` called `ComputeShader.HasKernel` and `FindKernel` before the common bake dispatch `try/finally`.
Solution: Add `TryResolveComputeKernel` with null, missing-kernel, invalid-index, and supported Unity exception handling.
Rejected Alternatives: Leave raw `HasKernel`/`FindKernel` calls inline, or wrap the whole method in one broad catch.
Scalability potential: Low/Middle/High/Ultra fail deterministically before GPU allocation when compute assets are damaged or unsupported.
Hardware Impact: Runtime 0 us. Editor avoids wasted RenderTexture/Texture2D allocation.

## Decision 85 - Mesh UV Transactions Must Not Catch Programmer Faults

Problem: Mesh UV remap/capture/preflight caught every non-`FatalArchitectureException`.
Solution: Replace broad filters with `IsRecoverableEditorException`, limited to Unity/IO/access/argument/operation/unsupported Editor failures.
Rejected Alternatives: Broad catches or growing fatal exclusion lists.
Scalability potential: Low/Middle/High/Ultra keep recoverable artist-facing failures while real defects stay visible.
Hardware Impact: Runtime 0 us.

## Decision 86 - Packed Rect Lookup Must Be Transactional

Problem: Missing packed rect data threw from atlas blit and mesh UV remap, including after rollback snapshots existed.
Solution: Replace throwing lookup with `TryFindPackedRectForSource`; mesh remap restores snapshots before returning false.
Rejected Alternatives: Keep the throw or catch fatal exceptions outside.
Scalability potential: Low/Middle/High/Ultra get deterministic atlas rollback when packed data is corrupt.
Hardware Impact: Runtime 0 us.

## Decision 87 - Empty Rectangle Packs Are Not Valid Atlases

Problem: `TryPackRectangles` accepted zero rectangles and returned success with efficiency `0`.
Solution: Add `inputs.Length == 0` guard and source-level functional test.
Rejected Alternatives: Rely on `TryPackTextureSets` only.
Scalability potential: Low/Middle/High/Ultra avoid false no-op atlas success states.
Hardware Impact: Runtime 0 us. Editor avoids downstream no-op work.

## Decision 88 - APEX Must Guard Transaction Safety Tokens

Problem: APEX verifier protected memory ceilings but not transaction hardening tokens.
Solution: Add `s_requiredTransactionSafetyTokens` and `VerifyRequiredTransactionSafetyTokens`, skipping the verifier file.
Rejected Alternatives: Keep checks only in EditMode probes.
Scalability potential: Low/Middle/High/Ultra keep transaction integrity across future atlas/bake edits.
Hardware Impact: Runtime 0 us. Cold verifier source pass only.

## Decision 89 - Mesh Buffer Shortage Must Trigger Rollback, Not Fatal Escape

Problem: `CopyVertexBufferFromMesh` threw `FatalArchitectureException` for short Unity vertex buffers, bypassing rollback for earlier meshes in a multi-mesh transaction.
Solution: Throw `InvalidOperationException` so `TryRemapMeshUvs` returns false and outer rollback restores snapshots.
Rejected Alternatives: Keep fatal throw or catch fatal broadly.
Scalability potential: Low/Middle/High/Ultra avoid partial UV mutation on inconsistent imported mesh data.
Hardware Impact: Runtime 0 us.

## Decision 90 - Mesh UV Remap Saves Once Per Atlas Transaction

Problem: `TryRemapMeshUvs` saved assets after every mesh remap.
Solution: Remove per-mesh `AssetDatabase.SaveAssets()` and rely on `TryFinalizeAtlasTransaction` after all remaps succeed; rollback restore still saves snapshots on failure.
Rejected Alternatives: Immediate per-mesh persistence.
Scalability potential: Low/Middle/High/Ultra reduce Editor I/O; high/ultra multi-mesh atlas batches save once instead of once per mesh.
Hardware Impact: Runtime 0 us. Estimated 200-5000 us saved per extra mesh depending on disk/project state.

## Decision 91 - Atlas Material Must Be Byte-Rollbacked With Texture Outputs

Problem: Atlas transaction rollback captured PNG outputs but not the generated/updated `.mat` file bytes and `.meta`.
Solution: Add a four-asset rollback overload and capture `materialPath` with albedo/normal/M.R.A.O. outputs before atlas writes.
Rejected Alternatives: Trust object-level material rollback only; it misses file-level corruption and newly-created material metadata.
Scalability potential: Low/Middle/High/Ultra keep atlas family assets recoverable when high-volume batch packing fails after material mutation.
Hardware Impact: Runtime 0 us. Editor failure recovery avoids manual reimport/rebuild work; estimated 100-3000 us saved per failed transaction plus asset-state integrity.

## Decision 92 - Readable Texture Import Bridge Must Fail Closed

Problem: Atlas source read fallback restored `TextureImporter.isReadable` in `finally`, but restore failure was warning-only.
Solution: Replace warning-only restore with `TryRestoreTextureReadableState`; atlas read success is denied if source texture readability cannot be restored.
Rejected Alternatives: Continue after warning; that leaves source textures readable and bloats imported asset memory.
Scalability potential: Low/Middle/High/Ultra protect compact/editor memory budgets while still allowing cold readable bridge imports for non-readable art sources.
Hardware Impact: Runtime 0 us. Prevents persistent Editor/import memory retention from readable texture CPU copies.

## Decision 93 - Mobile Atlas Compression Needs iOS ASTC Ownership

Problem: Texture import enforcement set Standalone BC7/BC5 and Android ASTC, but iOS/handheld import settings could fall back to platform defaults.
Solution: Add explicit `iPhone` ASTC_6x6 platform settings and audit `iPhoneCorrect` with the same max size as Android.
Rejected Alternatives: Rely on Unity default platform fallback or use uncompressed/readable source defaults.
Scalability potential: Low/Middle/High/Ultra keep generated atlas families compressed on mobile-class lanes without changing material or runtime logic.
Hardware Impact: Runtime 0 us. Prevents platform importer fallback that can waste VRAM on handheld/mobile builds.

## Decision 94 - Atlas Size Must Consume GlobalQualityWeight

Problem: Atlas size resolution was effectively hardwired to visual-overkill weight `1f` unless callers manually pre-resolved the atlas size.
Solution: Add `ResolveSafeAtlasSize(int,float)` and a non-breaking `TryPackTextureSets` overload that resolves `atlasSize = safeAtlasSize` before packing and allocation.
Rejected Alternatives: Keep fail-only behavior for oversized requests or introduce a binary low/high atlas switch.
Scalability potential: Low uses 512-safe atlas lanes when authored source allows it; Middle can land around 2K; High/Ultra can keep 4K, all from continuous `GlobalQualityWeight`.
Hardware Impact: Runtime 0 us. Editor bake memory scales down before scratch allocation and PNG encode.

## Decision 95 - Normal Bake Must Not Use Raw Normalize

Problem: The compute normal kernel used raw `normalize` on a generated tangent-space normal vector.
Solution: Add `HectonSafeNormalize` with `isfinite(lenSq)` and epsilon guard, falling back to `(0,0,1)`.
Rejected Alternatives: Keep raw normalize because z=1 usually makes the vector safe.
Scalability potential: Low/Middle/High/Ultra avoid invalid BC5 normal output when corrupted profile values or driver edge cases reach the kernel.
Hardware Impact: Runtime 0 us. Offline bake ALU change is negligible; failure mode becomes deterministic.

## Decision 96 - Mobile Import Audit Must Prove Both Handheld Lanes

Problem: The importer wrote Android and `iPhone` overrides, but meta audit could still pass with Standalone compression evidence only.
Solution: Add Android and `iPhone` platform regex probes and require `mobileOverrides` in `AuditTextureMeta`.
Rejected Alternatives: Trust the setter path or only audit Android; both allow mobile regression if Unity serialization changes or one platform setting is dropped.
Scalability potential: Low/Middle keep ASTC mobile atlas memory controlled; High/Ultra keep the same atlas family route without per-platform material forks.
Hardware Impact: Runtime 0 us. Prevents false-positive proof that can leave handheld builds on importer fallback.

## Decision 97 - Atlas Authoring Size Must Normalize Before Pack Rejection

Problem: `TryPackTextureSets` rejected positive non-power or oversized authoring atlas sizes before `GlobalQualityWeight` could clamp them.
Solution: Reject only non-positive sizes immediately, then resolve through `ResolveSafeAtlasSize` before rectangle packing and allocation.
Rejected Alternatives: Force callers to pre-normalize or keep a binary valid/invalid size gate.
Scalability potential: Low can land on 512/1024 lanes; Middle can land around 2K; High/Ultra can retain 4K, all from one continuous quality route.
Hardware Impact: Runtime 0 us. Editor bake avoids avoidable hard failure and scales scratch/encode memory before allocation.
