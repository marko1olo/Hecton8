# SHINOBU_348 Screen-Space PDA Projector Route Card

Status: `STATIC_ROUTE_DOC / RUNTIME_PROOF_PENDING`
Evidence class: `STATIC_DOC / STATIC_SOURCE`
Owner domain: presentation/UI/PDA projection
Review disposition: `YELLOW / STATIC_DOC_ONLY` until compile/import/runtime/profiler/player proof exists.

Owner: `SHINOBU_348`
Domain: Echelon 8 Presentation & UX, wrist-mounted PDA projection

## Route

`WristHologramHudRuntime` cold owner setup acquires Vault handles and graphics buffers.

Visual-sync route: `PdaProjectionInputDTO[1]` -> `CompilePdaMatricesJob` -> camera-relative `PdaStateDTO[1]` after double AUP subtraction -> mapped `UnsafeUtility.MemCpy` into double-buffered `GraphicsBuffer` rows -> `WristPdaScreenProjectorFeature` RenderGraph pass -> `Hecton_PdaScreen.shader`.

Graphics route cold gates:

- `SystemInfo.supportsSetConstantBuffer`;
- `SystemInfo.graphicsShaderLevel >= 45`;
- shader `#pragma target 4.5`;
- `StructuredBuffer<PdaStateDTO>` ABI.

Unsupported mobile/GLES-era targets release PDA graphics buffers and fail closed.

- Shader path: reconstruct camera ray per pixel, intersect uploaded PDA plane, resolve atlas UVs, sample `_CameraDepthTexture`, and apply `GlobalQualityWeight` glass refraction.
- Quality below `0.20` uses one direct atlas sample; `0.20..0.36` fades in refraction, and `0.52..0.88` fades in chromatic taps through uniform math LOD.
- There is no wrist World-Space Canvas route in this projector.

## Vault Buffers

- `348730` `PdaStateDTO[1]`, explicit 80-byte GPU state, uninitialized.
- `348731` `PdaProjectionInputDTO[1]`, explicit 112-byte AUP/orientation input, uninitialized.
- `348732` `PdaProjectionTelemetryEntry[300]`, explicit 64-byte black-box ring, uninitialized and overwritten by owner.
- `348733` `int[1]`, telemetry cursor.
- `348734` `PdaProjectionTuningDTO[1]`, explicit 64-byte art tuning row.
- `348735` `PdaInterfaceProfileDTO[64]`, explicit 32-byte atlas UV profiles.
- `348736` `byte[16384]`, cold CSV scratch mirror for `pda_interface_profiles.csv`.

- On file platforms, the CSV ingestor reads `Assets/StreamingAssets/Hecton8/PDA/pda_interface_profiles.csv` directly into the `348736` Vault scratch row through `FileStream.Read(Span<byte>)`, then parses `ReadOnlySpan<byte>` over the unmanaged scratch.
- URI-backed StreamingAssets targets, including Android/Quest APK paths, do not attempt gameplay `FileStream` or `UnityWebRequest` staging.
- They fail closed to owner-seeded deterministic default row until DataMonolith/binary import route is baked and boot-validated.
- Repo-root `pda_interface_profiles.csv` remains an editor/development fallback only.
- Committed packaged source contains one `default` atlas rect plus canonical rows.
- Rows: `inventory`, `loadout`, `construction`, `barter`, `data_log`, `spectrum`, `atlas_signal`, `diagnostics`.
- Purpose: physical direct-file cold-boot input for CI/art tuning.
- It maps `tab_#`, `pda_tab_#`, and canonical PDA tab names through the same `ResolvePdaTabHash(int)` path as `PDAEventPayload.CurrentTab`; unknown authored names retain FNV fallback.
- It does not borrow the legacy managed HUD CSV byte array and does not claim `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` readiness.
- If no direct-file packaged/editor CSV exists, the owner-seeded default row remains deterministic and rows `1..63` are explicitly zeroed before any lookup scans them.

- Shader does not reconstruct absolute world rays.
- Pixel rays come from `UNITY_MATRIX_I_P` in view space.
- Camera-relative PDA basis rotates through `UNITY_MATRIX_V`.
- Depth occlusion compares scene `LinearEyeDepth` against `-hit.z`.

## Rollback Exclusion

These buffers are presentation-only.

`PdaStateDTO` contains sub-millimeter wrist screen pose, boot progress, and visual flags.

Forbidden sinks: `StateRingBuffer`, Merkle hashing, save identity, rollback truth, gameplay authority. Authoritative routes remain inventory/PDA gameplay state and PDA event lanes.

## Black Box

- The projector records the last 300 frames in `PdaProjectionTelemetryEntry`.
- Non-finite matrix input or matrix compilation cost above `100 us` dumps the ring to `Docs/AgentLogs/Dump_SHINOBU_348.bin` in Editor and to `Application.persistentDataPath/Hecton8/AgentLogs/Dump_SHINOBU_348.bin` in player builds.
- Dump header version `2` is explicit 64-byte row.
- Fields: valid-count and start-index.
- Fault writer clears ring at cold seed.
- It writes valid telemetry rows oldest-to-newest, not raw uninitialized capacity.

## Read/Write Discipline

- Mutation routes use generation-checked `TryResolveHandle` only inside the owner write phase.
- Public/editor read routes use `TryReadOnlyHandle` through `TryReadOnlyPdaProjectionVaultBuffer`, so `TryGetActivePdaProjectionTuning`, `TryGetActivePdaProjectionTelemetry`, and gizmo reads cannot expose mutable Vault rows, create buffers, or grow buffers.
- Fault dumps use legacy `TryReadHandle` validation only to obtain a raw read pointer for binary export. Dump execution is not a public accessor.

Selected-object SceneView gizmo treats `PdaStateDTO.LocalToWorld` as camera-relative presentation truth.

It adds resolved render camera position only to gizmo matrix translation. It does not mutate Vault DTO, upload absolute-world debug row, or change shader route.

`PdaProjectorLateFrameTick` hot path:

- Does not generate Vault rows.
- Does not create graphics buffers.
- Checks ready flags.
- Fails closed until cold setup completes.
- Patches telemetry cost through already-open ring/cursor arrays.
- Performs no second Vault resolve after matrix compilation.

Mock wrist projection and forced PDA visibility serialize false by default.

Task 06 mock generation remains editor/emergency-only. Mock input is compiled out for non-development player builds, so production PDA cannot stay visible or pay the pass while closed.

`TrySetActivePdaProjectionTuning` compiles only in Editor. Player builds keep pure read accessors and render resource query; designer tuner cannot mutate Vault tuning there.

## Shader Warmup

PDA shader warmup:

- Variant: no-keyword `Hidden/Hecton8/Hecton_PdaScreen`.
- Asset: `Assets/_Project/Art/Shaders/Variants/Hecton_PdaScreen_Warmup.shadervariants`.
- Bootstrap: `00_BOOTSTRAP.unity` serializes it through `BootstrapController.shaderVariantCollections`.
- Prewarm: `GameBootstrapper.WarmConfiguredShaderVariantCollectionsAsync` calls `WarmUp()` before gameplay activation.
- Renderer feature uses the serialized shader asset path; no `Shader.Find`, no fallback material allocation.

## Renderer Activation

`WristPdaScreenProjectorFeature` is serialized active in every currently referenced URP renderer asset:

- `Assets/_Project/Data/PC_Renderer.asset` local fileID `348348348000001`.
- `Assets/_Project/Data/PC_High_Renderer.asset` local fileID `348348348000002`.
- `Assets/_Project/Data/Mobile_Renderer.asset` local fileID `348348348000003`.
- `Assets/_Project/Data/Quest_VR_Renderer.asset` local fileID `348348348000004`.

Each asset inserts the projector before `HectonVisorUberPostFeature`.

PDA projection participates in the visor/post stack instead of drawing after it. `m_RendererFeatureMap` was regenerated as little-endian signed 64-bit fileIDs and verified against each `m_RendererFeatures` list.
