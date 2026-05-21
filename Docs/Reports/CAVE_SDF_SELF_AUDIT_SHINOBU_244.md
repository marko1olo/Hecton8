# SHINOBU_244 Static Cave SDF Self Audit

Status: PENDING_COMPILE_VERIFICATION
EvidenceClass: STATIC_SOURCE / FILESYSTEM ONLY

<SELF_AUDIT agent="SHINOBU_244" role="STATIC_CAVE_SDF_VOLUME_BAKER">
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS">Environment runtime SDF generation scan recorded in Status; owned code adds no runtime generator.</TASK>
    <TASK id="02" status="PASS">Physics proximity scanner exists and reports method-context MeshCollider proximity debt for owning agents; SHINOBU_244 did not edit foreign runtime domains.</TASK>
    <TASK id="03" status="PASS">Burst DTOs use raw public fields; static scan shows no get/set properties in the owned baker path.</TASK>
    <TASK id="04" status="PASS">TriangleDTO is explicit 48 bytes: V0=0, V1=12, V2=24, Normal=36.</TASK>
    <TASK id="05" status="PASS">GenerateMockTorusMeshJob creates dense twisted torus triangle input for isolated stress testing.</TASK>
    <TASK id="06" status="PASS">ConstructBvhJob builds a flat native BVH with in-place triangle index partitioning and stack/node capacity fallback to leaf nodes.</TASK>
    <TASK id="07" status="PASS">EvaluateSdfVolumeJob queries the BVH per voxel, computes closest point distance, and signs with +X parity.</TASK>
    <TASK id="08" status="PASS">CompressSdfToHalfJob uses math.f32tof16 into NativeArray&lt;ushort&gt;.</TASK>
    <TASK id="09" status="PASS">Closest traversal starts at MaxSdfDistance squared and prunes AABB nodes beyond the narrow band.</TASK>
    <TASK id="10" status="PASS_WITH_DEVIATION">Binary writer emits 64-byte little-endian header plus explicit little-endian ushort payload through an editor-blocking chunked FileStream path, verifies temp size, preserves prior payload as .bak, then renames .tmp to .h8bin. Async native-pointer write was rejected after compile-risk review because TempJob memory must not cross await boundaries.</TASK>
    <TASK id="11" status="PASS">Optional Texture3D output checks RHalf/R16_SFloat sampling support, then uses GraphicsFormat.R16_SFloat and Texture3D.SetPixelData when supported.</TASK>
    <TASK id="12" status="PASS">Header stores double3 AUP anchor and float3 bounds min/max for local field reconstruction.</TASK>
    <TASK id="13" status="PASS">Architecture doc fences .h8bin SDF data out of rollback/Merkle state.</TASK>
    <TASK id="14" status="PASS">Large NativeArray bake buffers use NativeArrayOptions.UninitializedMemory.</TASK>
    <TASK id="15" status="PASS">Bake pipeline writes CAVE_SDF_BAKE_REPORT.json after generation and records 300 local TempJob telemetry rows before dump.</TASK>
    <TASK id="16" status="PASS">Static SDF Forge UI Toolkit window provides mesh field, resolution, band, submesh, quality, AUP, bake, benchmark, and scanner controls.</TASK>
    <TASK id="17" status="PASS">sdf_baking_profiles.csv bridge and bounded Span parser exist; stackalloc is capped at 4 KB with ArrayPool fallback; profile-capacity overflow fails closed; no string.Split/LINQ parser.</TASK>
    <TASK id="18" status="PASS_WITH_DEVIATION">StaticCaveSdfSliceSceneOverlay replaces an OnDrawGizmos-style component with SceneView.duringSceneGui, streaming rows from the last generated .h8bin preview file without persistent preview NativeArray ownership or an Editor-only MonoBehaviour scene component.</TASK>
    <TASK id="19" status="PASS_WITH_DEVIATION">Physics_Proximity_Scanner writes SHINOBU-specific method-context text-scan report and preserves another agent's shared report artifact instead of clobbering PHYSICS_OPTIMIZATION_REPORT.json.</TASK>
    <TASK id="20" status="PASS_WITH_COMPILE_PENDING">Self-audit/doc/log artifacts exist; compile/import/Burst Inspector proof remains blocked by CPU gate.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <STRUCT name="TriangleDTO" size="48" multipleOf="16">
      <FIELD name="V0" offset="0" size="12" />
      <FIELD name="V1" offset="12" size="12" />
      <FIELD name="V2" offset="24" size="12" />
      <FIELD name="Normal" offset="36" size="12" />
      <PADDING bytes="0" />
      <MATH>12+12+12+12=48; 48 mod 16 = 0; no Pack=1.</MATH>
    </STRUCT>
    <STRUCT name="BvhNodeDTO" size="64" multipleOf="64">
      <FIELD name="BoundsMin" offset="0" size="12" />
      <FIELD name="BoundsMax" offset="12" size="12" />
      <FIELD name="Left" offset="24" size="4" />
      <FIELD name="Right" offset="28" size="4" />
      <FIELD name="TriangleStart" offset="32" size="4" />
      <FIELD name="TriangleCount" offset="36" size="4" />
      <FIELD name="Depth" offset="40" size="4" />
      <FIELD name="Flags" offset="44" size="4" />
      <FIELD name="_pad0" offset="48" size="8" />
      <FIELD name="_pad1" offset="56" size="8" />
      <MATH>48 data bytes + 16 explicit pad bytes = 64; one cache line.</MATH>
    </STRUCT>
    <STRUCT name="StaticCaveSdfBakeConfigDTO" size="96" multipleOf="32">
      <FIELD name="AnchorAup" offset="0" size="24" />
      <FIELD name="BoundsMin" offset="24" size="12" />
      <FIELD name="BoundsMax" offset="36" size="12" />
      <FIELD name="Resolution" offset="48" size="12" />
      <FIELD name="MaxSdfDistance" offset="60" size="4" />
      <FIELD name="GlobalQualityWeight" offset="64" size="4" />
      <FIELD name="SubMeshIndex" offset="68" size="4" />
      <FIELD name="VoxelCount" offset="72" size="4" />
      <FIELD name="TriangleCount" offset="76" size="4" />
      <FIELD name="Flags" offset="80" size="4" />
      <FIELD name="_pad0" offset="84" size="4" />
      <FIELD name="_pad1" offset="88" size="8" />
      <MATH>All double/ulong fields start on 8-byte offsets; 96 mod 32 = 0.</MATH>
    </STRUCT>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>
    GlobalQualityWeight is continuous and does not change gameplay truth, file identity, DTO layout, or rollback route. It drives editor bake work shape only: BVH leaf triangles lerp from 16 at q=0 to 4 at q=1 via smoothstep, SDF batch size lerps from 256 to 32, and compression batch size lerps from 512 to 128. Low/MX350 editor bakes reduce scheduling overhead; High/Ultra spends more scheduling granularity on load balance and visual-overkill Texture3D output. Runtime consumers must scale query cadence/fidelity in their own owner routes.
  </SCALABILITY_CURVE>
  <CONFIG_SANITIZATION_PROOF>
    SanitizeConfig clamps resolution through a 64-bit voxel-count guard, clamps non-finite MaxSdfDistance to a finite 0.05m..50000m range, validates explicit or Unity mesh bounds before use, falls back to a finite 1m cube only when no valid bounds exist, and rejects mesh-local centers or half-extents beyond the 100km authoring budget. AUP owns universe offset; local SDF bounds are therefore prevented from entering Burst/header math as NaN, infinity, or astronomical float spans.
  </CONFIG_SANITIZATION_PROOF>
  <H_PHI_VAULT_STATUS>
    Runtime VaultBufferHandle IDs requested by SHINOBU_244: none. Reason: this agent owns offline Editor baking only; runtime streaming into GlobalDataVault belongs to Agent 12/134/157 or terrain streaming owner. Runtime persistent private NativeArrays introduced: zero. Editor persistent private NativeArrays introduced: zero. Editor preview private vertex arrays introduced: zero. Blackbox telemetry is a local TempJob buffer owned by the bake call and disposed in finally; live slice preview streams rows from the generated .h8bin file and draws discs instead of retaining a private NativeArray or managed vertex-array copy.
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    Burst jobs mark non-overlapping NativeArray/NativeSlice inputs/outputs with ReadOnly, WriteOnly, and NoAlias. Mesh conversion now receives a per-submesh NativeSlice and each scheduled worker writes Output[triangleIndex] inside that slice, so parallel-for safety suppressions are zero. GenerateMockTorusMeshJob, EvaluateSdfVolumeJob, ValidateSdfDistanceWarningsJob, and CompressSdfToHalfJob also use safe NativeArray writes. Editor bake handle order is BuildTrianglesFromMesh16Job or BuildTrianglesFromMesh32Job -> ConstructBvhJob -> EvaluateSdfVolumeJob -> ValidateSdfDistanceWarningsJob -> CompressSdfToHalfJob -> binary/Texture3D serialization. There is no runtime dispatcher handle because this is an offline Editor pipeline; same-frame Complete fences are editor bake stage barriers, not gameplay Tick barriers.
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    Hecton8.World.StaticCaveSdfBaker.asmdef references Unity.Mathematics only. Hecton8.World.StaticCaveSdfBaker.Editor.asmdef references the owned baker assembly plus Unity.Burst, Unity.Collections, Unity.Jobs, and Unity.Mathematics. No sibling runtime domain reference, GlobalRegistry slot, SignalBus lane, or runtime controller is introduced. StaticCaveSdfContracts.cs is DTO/constants-only; editor-only finite/mix/hash helper code lives outside the runtime contract surface.
  </COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>
    Rejected runtime PhysX/MeshCollider proximity and runtime point-to-triangle cave distance. Replacement fake: offline BVH bake writes immutable half-float signed distance grids and optional R16 volume texture. Before: runtime geometric awareness trends O(queryCount * triangleCount) or broadphase-dependent PhysX cost. After: one offline O(voxelCount * log(triangleCount)) bake plus runtime O(1) field sampling by the owning consumer.
  </DEAR_LIE_CONFIRMATION>
  <SERIALIZATION_PROOF>
    Writer contract: `.tmp` write, byte-count verification (`64 + voxelCount * 2`), explicit little-endian header and half-distance ushort payload, stale `.bak` cleanup through DeleteExistingBackupOrThrow, previous `.h8bin` moved to `.bak`, final rename, backup restore attempt on failed rename, and stale `.tmp` cleanup through DeleteStaleTempBestEffort on failed write/size/rename. Bake report records expectedFileSizeBytes, endianness, payloadEndian, atomicWrite, compileStatus, and unityImportProof fields.
  </SERIALIZATION_PROOF>
  <STATIC_GATE_PROOF>
    Latest source scan over SHINOBU_244 returned no hits for mesh.vertices, Physics.ClosestPoint, Physics.Raycast(, MeshCollider, get/set DTO properties, async void, Pack=1, GlobalRegistry, TryGetLatestCreated, or direct final-path delete. The scanner assembles forbidden finding tokens from neutral pieces so proof gates detect real runtime usage instead of the cold reporting tool.
  </STATIC_GATE_PROOF>
  <NAN_VACCINATION_PROOF>
    EvaluateSdfVolumeJob now uses guarded reciprocals for degenerate triangle closest-point denominators and signed ray-parity determinant reciprocals. Ray parity applies a deterministic sub-millimeter YZ offset before BVH traversal so rays do not double-count or miss shared triangle edges/vertices on stable grid samples. The closest loop no longer stores unused best-point/best-normal values because signedness is decided by parity. BVH traversal stack overflow writes a finite out-of-band distance sentinel instead of dropping child nodes or relying on FastMath NaN propagation; the validator clamps non-finite or out-of-band values, writes WarningNonFiniteFallback, and triggers Dump_SHINOBU_244.bin after Stage2 telemetry is recorded. Remaining raw rcp sites are guarded by max(count,1), max(resAxis-1,1), or the safe reciprocal helpers. ValidateSdfDistanceWarningsJob remains the single writer for the one-int TempJob warning lane.
  </NAN_VACCINATION_PROOF>
  <COLD_EDITOR_IO_HYGIENE>
    The editor-only binary writer returns its 64 KiB ArrayPool byte buffer with clearArray=true after copying native half payload chunks. The CSV profile loader also clears rented byte buffers before returning them to the shared pool. Blackbox dump rows allocate their stack row buffer from UnsafeUtility.SizeOf&lt;StaticCaveSdfTelemetryEntry&gt; instead of a hard-coded byte count. Forge-generated XML proof text escapes generic angle brackets instead of emitting raw `SizeOf&lt;T&gt;` syntax inside XML nodes. Physics_Proximity_Scanner now walks directories with an explicit pending-directory stack, fences file and directory enumeration separately, and writes scanIncomplete plus diagnostics entries so one locked or denied folder cannot silently truncate the report.
  </COLD_EDITOR_IO_HYGIENE>
  <CSV_SCHEMA_PROOF>
    StaticCaveSdfProfileCsvParser validates the exact header order `name,resolution,narrow_band_meters,global_quality_weight,submesh_index` before parsing profile rows. Reordered or malformed headers fail closed and emit row 1 / column diagnostics instead of silently mapping designer values into the wrong fields. Each data row also validates a non-empty profile name, required comma boundaries, integer/float field formats, integer overflow, row ending, and capacity overflow beyond 16 profiles; file length races or IO/permission races fail closed during cold CSV load; malformed rows fail the import closed with row/column diagnostics instead of producing clamped default bake recipes or silently ignored designer rows. Profile byte hashing is owned by StaticSdfForgeWindow in the Editor assembly; StaticCaveSdfContracts.cs contains DTOs/constants only and no string-hash utility.
  </CSV_SCHEMA_PROOF>
  <SELF_AUDIT_GENERATION_PROOF>
    StaticCaveSdfBakePipeline.WriteSelfAudit now emits the rich schema used by this artifact: EvidenceClass, XML task reconciliation, struct layout verification, payload format, serialization proof, compile status, static-gate caveat, deviation register, non-finite warning proof, CSV schema proof, mesh input guard proof, cold editor IO hygiene, editor preview boundary proof, editor sync-barrier proof, and read-accessor hygiene. A future Forge bake should not overwrite this report with the older reduced template.
  </SELF_AUDIT_GENERATION_PROOF>
  <BVH_CAPACITY_GUARD_PROOF>
    ConstructBvhJob exits before construction when TriangleIndices, Nodes, or Stack are not created or when Stack/Nodes have zero capacity. Before publishing child links it checks nodeCount + 2 and stackCount + 2 against fixed capacities. Insufficient capacity converts the current node to a leaf and sets a warning flag, preventing a parent from referencing child nodes whose ranges were never pushed.
  </BVH_CAPACITY_GUARD_PROOF>
  <MESH_INPUT_GUARD_PROOF>
    BuildTrianglesFromMeshData rejects unreadable meshes and catches Unity/argument failures from Mesh.AcquireReadOnlyMeshData before returning a guarded false result. ReadSubMeshRange rejects negative starts/counts, zero counts, descriptor overflow, out-of-capacity spans, and non-triangle-multiple index counts instead of repairing corrupt imported descriptors through clamp/truncate. All-submesh mode skips non-triangle topology but fails closed on any corrupt triangle submesh descriptor instead of silently baking a partial mesh. Mesh conversion is split into BuildTrianglesFromMesh16Job and BuildTrianglesFromMesh32Job, so no scheduled job carries a default index NativeArray. Both variants receive MeshData vertexCount and active submesh IndexCount, write through a per-submesh NativeSlice, validate triangleIndex against the slice before raw index or position reads, reject absolute index reads outside the active submesh span or outside the active index NativeArray length, reject UInt32 index values above Int32.MaxValue before baseVertex adjustment, prevent invalid index fallback from inheriting baseVertex, reject baseVertex overflow through 64-bit arithmetic, reject negative/upper-bound vertex indices, and validate the computed byte range before raw strided vertex reads. Malformed baseVertex/index data collapses to a finite zero vertex instead of reading outside the MeshData position stream. Every owned IJobParallelFor Execute method now guards output range at the job boundary; EvaluateSdfVolumeJob fail-closes missing triangle/index/node inputs through the traversal-failure sentinel, guards resolution layer multiplication, and CompressSdfToHalfJob guards input/output length mismatch with a zero fallback. ConstructBvhJob rejects triangle-index buffers shorter than the triangle stream, and EvaluateSdfVolumeJob bounds-checks BVH leaf index ranges before reading TriangleIndices. The all-submesh path iterates triangle submeshes separately, preserves each submesh baseVertex, accumulates total triangles in 64-bit space before native allocation, and rejects triangle streams that would overflow fixed BVH node capacity.
  </MESH_INPUT_GUARD_PROOF>
  <TEXTURE3D_GUARD_PROOF>
    Optional Texture3D emission checks SystemInfo.supports3DTextures, TextureFormat.RHalf, and GraphicsFormat.R16_SFloat sample support before asset creation. Unsupported format support skips the optional visual-overkill texture and leaves the immutable .h8bin as the authoritative payload.
  </TEXTURE3D_GUARD_PROOF>
  <EDITOR_PREVIEW_BOUNDARY_PROOF>
    Live slice preview is an Editor SceneView overlay, not a MonoBehaviour component. It draws per-sample discs through Handles.DrawSolidDisc and does not retain a private preview vertex array. Preview stream open/read races during a new bake or file rename fail closed by returning null/false instead of throwing SceneView GUI exceptions. Invalid row starts and row widths fail closed before offset/read math, so malformed preview requests cannot overflow the row byte count or seek outside the payload. No SHINOBU_244 preview component can be attached to runtime scenes or prefabs, so player builds do not inherit missing-script references from this tool.
  </EDITOR_PREVIEW_BOUNDARY_PROOF>
  <EDITOR_SYNC_BARRIER_PROOF>
    Owned .Complete() and AssetDatabase sync sites are labeled [EDITOR_BLOCKING_SYNC_POINT]. They are offline Forge stage barriers for MeshData lifetime, BVH counters, stage timing, payload serialization, binary import, optional Texture3D asset creation, and AssetDatabase save/refresh, not gameplay Tick or SystemDispatcher routes.
  </EDITOR_SYNC_BARRIER_PROOF>
  <READ_ACCESSOR_HYGIENE>
    Mutating/allocating Editor helpers use action names: BuildTrianglesFromMeshData, LoadProfilesFromCsv, ParseProfileRow, ParseKeyHash, ParseInt, ParseFloat, ValidatePreviewBinaryForGizmo, DeleteExistingBackupOrThrow, DeleteStaleTempBestEffort, and CopyRowFromOpenStreamForGizmo. Remaining ResolveVoxelPosition, ReadIndex, ReadPosition, ReadSubMeshRange, and TryGetForbiddenSymbol are pure local computations or bounded local array/range reads with no global mutation, allocation, job completion, or scene search.
  </READ_ACCESSOR_HYGIENE>
  <DEVIATION_REGISTER>
    Task10 async serialization was replaced with an editor-blocking synchronous writer because the bake caller blocks and the payload is TempJob/native memory. Task18 OnDrawGizmos was replaced with SceneView.duringSceneGui to prevent player-scene missing-script debt. Task19 shared report output was replaced with a SHINOBU-specific report to preserve SHINOBU_227's existing artifact. Scanner proof is method-context streaming text scanning, not Roslyn AST.
  </DEVIATION_REGISTER>
  <RUNTIME_CONTRACT_SURFACE>StaticCaveSdfContracts.cs now contains DTOs and constants only. StaticCaveSdfMath and HashBytes are absent from owned source. StaticSdfForgeWindow owns HashProfileByte for fallback and CSV profile hashing inside the Editor assembly.</RUNTIME_CONTRACT_SURFACE>
  <STATIC_GATES>Loop 65 current static gates after all-submesh corrupt triangle descriptor fail-closed hardening: JSON report parses; self-audit XML fragment parses; generated/static report schema scan finds allSubmeshesCorruptTriangleDescriptorFailsClosed, uint32IndexOverflowRejected, configSanitization, CONFIG_SANITIZATION_PROOF, csvFileLengthRaceFailsClosed, previewRowBoundsOverflowFailsClosed, payloadEndian, bigEndianHostSwapFallback, evaluatorMissingInputsFailClosed, scenePreview, editorSyncBarriers, readAccessorHygiene, previewIoRaceFailsClosed, completeOrSyncSiteCount, blackboxDumpUsesTelemetryStructSize, and selfAuditXmlEscapesGenericProof; forbidden source scan no hits; NativeDisableParallelForRestriction=0; BURST_REQUIRED=7; SYNC_SITES=10; LABEL_TOKENS=10; StaticCaveSdfBakePipeline.cs brace count 136/136; scoped git diff --check passes for tracked files/untracked tolerated; untracked source/docs/report whitespace and final-LF gates pass. Historical FormatUsage.Sample proof is superseded; current source uses GraphicsFormatUsage.Sample for direct SystemInfo.IsFormatSupported.</STATIC_GATES>
  <COMPILE_STATUS>NOT_RUN_CPU_GATE. Process scan found no dotnet/csc/VBCSCompiler process, but the latest CPU gate sampled 100 percent; project rule forbids dotnet/csc while CPU is above 50 percent.</COMPILE_STATUS>
</SELF_AUDIT>
