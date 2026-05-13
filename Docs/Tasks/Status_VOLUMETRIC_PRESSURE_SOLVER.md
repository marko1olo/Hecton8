# Status: VOLUMETRIC_PRESSURE_SOLVER

Agent: HABITAT_ARCHITECT  
Domain: ECHELON 6 HABITAT & VEHICLES - Structural Integrity Math / Habitat Deformation  
Task Count: 19  
State: PENDING VERIFICATION - GLOBAL COMPILE WALL OUTSIDE OWNED FILES

## Mandates Loaded

- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt` - shader deformation and normal-map crease are mandatory before physical mesh mutation.
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt` - no managed allocations in habitat stress tick or shader upload path.
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt` - MX350/i3 budget makes per-vertex fake acceptable, per-frame mesh writes rejected.
- `REND_URP_Graphics_HotPath_Optimization_HLOD.txt` - SRP-friendly buffer upload and shader LOD fallback required.
- `GPU_Compute_Kernels_Kernels_Optimization_MX350.txt` - structured GPU buffers must stay compact and predictable.
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt` - peak module stress must enter blackbox telemetry.
- `CORE_Damage_System_Hull_Integrity_VFX_Feedback.txt` - hull stress is signal-driven and clamped.
- `PHYS_Fluid_Incursion_Interior.txt` - pressure/flooding remains scalar gameplay truth plus visual fake.

## Titanium Tasks

- [x] 1. Extend `HabitatGraphManager`; no singleton introduced. DOD: stress matrix lives in existing graph owner; rejected new manager singleton/MonoBehaviour; estimate 8 us structural rebuild path, 0 us singleton lookup.
- [x] 2. Consume existing hull/deformation damage lanes without hard dependency. DOD: reads `SignalBus<HullDeformedSignal>`/`SignalBus<CombatDamageSignal>` snapshots and reuses `HullStressSignal` audio publication for fast stress deltas; rejected direct audio subsystem ownership; estimate 4 us/frame plus bounded signal scan.
- [x] 3. Add `Hecton8.Habitat.Deformation.Contracts` asmdef boundary. DOD: contracts-only asmdef + read-model contract created; rejected moving `HabitatGraphManager` into a new asmdef because current root assembly dependencies would break; estimate 0 us hot path.
- [x] 4. Dead code hunt for runtime `Mesh.vertices` habitat editing. DOD: scoped `rg` found no `Mesh.vertices`/`.vertices =` in Construction/BaseModule/Habitat owned paths; rejected CPU mesh mutation; estimate 0 us/frame.
- [x] 5. Add `NativeArray<float>` module stress matrix. DOD: `_moduleStressScalars`, previous-stress lane, spike lane, and hysteresis lane are persistent NativeArrays with sentinel registration/disposal; rejected managed `float[]`; estimate 1 us/module.
- [x] 6. Compute stress from depth, pressure, integrity damage, flood, and impact spike. DOD: `ResolveModuleStress01` uses ambient pressure depth scalar + integrity/joint/compression/flood impact + decaying spike; rejected rigidbody pressure sim; estimate 1 us/module.
- [x] 7. Upload scalar matrix to GPU with `GraphicsBuffer`. DOD: `GraphicsBufferUploadUtility.UploadNativeArray` copies persistent `NativeArray<float>` into `_HectonHabitatModuleStressBuffer`; rejected material property arrays; estimate 6-18 us/upload.
- [x] 8. Bind module stress index for rendering path. DOD: shader resolves module index against existing ambience buffer; BRG-specific packer not present in domain, so existing module render data path is used; rejected object-name lookup; estimate 2 us/module setup.
- [x] 9. Create `Hecton_HabitatInterior.hlsl` stress read path. DOD: include declares stress buffer/params and bounded stress index/read functions; rejected world-space mesh bend; estimate vertex-only on Mid/High/Ultra.
- [x] 10. Implement sine panel bulge math. DOD: object-space offset uses `sin(uv.x*pi)*sin(uv.y*pi)*Stress*MaxDeformation`; rejected simulation mesh dents; estimate 8-12 ALU/vertex.
- [x] 11. Sync fast stress deltas with structural acoustics. DOD: peak per-module delta publishes existing `HullStressSignal` parameters; rejected per-wall audio emitters; estimate 1 signal/event.
- [x] 12. Inject 1-second Leviathan impact spike into specific/nearest module. DOD: Leviathan/impact/pressure/microfracture signals set `_moduleImpactStressSpikes`, then decay at 1/sec; rejected physics impulse deformation; estimate one bounded scan/event.
- [x] 13. Keep deformation object/local-space and AUP-safe. DOD: vertex bend offsets `positionOS` along `normalOS`; only index selection uses runtime center buffer; rejected world-space offsets; estimate 0 us AUP rebase cost.
- [x] 14. Low-tier MX350 disables vertex bend, keeps crease overlay. DOD: low-tier publishes max deformation 0 and shader uses peak stress crease/detail overlay without per-module vertex index loop; rejected medium-ground deformation; estimate fragment overlay only.
- [x] 15. Prove zero-GC scalar upload. DOD: hot path uses persistent NativeArrays, fixed signal snapshots, no LINQ/string formatting/interpolation in new C# paths; rejected managed arrays; estimate 0 B/frame.
- [x] 16. Push `PeakModuleStress` into blackbox telemetry and dump on invalid state. DOD: `HabitatFloodBlackBoxEntry` version 3 records `PeakModuleStress`/deformation sequence and invalid stress dumps `Dump_VOLUMETRIC_PRESSURE_SOLVER.bin`; rejected blind crash reports; estimate ring write only.
- [x] 17. Emit `BaseModuleCompromisedSignal` at max threshold. DOD: added 64-byte signal contract/lane/config/publish method and graph-side hysteresis publish; rejected tight subsystem reference; estimate one bus publish.
- [!] 18. Compile/check shader buffer indexing. DOD: scoped grep confirms bounded buffer index path and no added exact normalize/sqrt; `dotnet build` blocked by 107 unrelated missing refs and Unity MCP reports `no_unity_session`; estimate n/a.
- [x] 19. Recursive normals verification and cheap normal bias. DOD: prompt re-read attempted from `CURRENT_BATCH.md` but tag was already absent; cheap normal bias added with rsqrt safe-normal helpers; final state remains `PENDING VERIFICATION`.

## Iterative Loop Ledger

- Loop 1: COMPLETE/BLOCKED BUILD - tasks 1-5 implemented; `dotnet build Hecton8.Core.csproj -v:minimal` blocked by 107 unrelated missing namespace/type errors before habitat verification.
- Loop 2: COMPLETE - tasks 6-10 implemented; scoped shader grep confirms stress buffer read/bulge path.
- Loop 3: COMPLETE - tasks 11-14 implemented; low-tier path avoids vertex module scan and uses crease overlay.
- Loop 4: COMPLETE/BLOCKED BUILD - tasks 15-18 implemented; compile gate blocked outside owned domain, Unity session unavailable.
- Loop 5: COMPLETE/PENDING VERIFICATION - recursive prompt re-read attempted, normals audited, exact `normalize` removed from new include, status remains PENDING VERIFICATION.

## Verification Notes

- `dotnet build Hecton8.Core.csproj -v:minimal`: FAILED with 107 unrelated missing namespace/type errors before habitat-specific validation.
- Unity MCP refresh/read console: FAILED, `no_unity_session`.
- Scoped mesh mutation scan: no runtime `Mesh.vertices` / `.vertices =` in Construction, BaseModule, or Habitat deformation contract paths.
- Scoped anti-bloat scan: no new string interpolation, `string.Format`, `.ToString()`, LINQ, `math.sqrt`, `math.normalize`, `.normalized`, or HLSL `normalize()` in owned additions. Existing cold constructor `new List`/`new Dictionary` lines in `HabitatGraphManager` predate this task.
- `CURRENT_BATCH.md` re-extraction after task execution: `VOLUMETRIC_PRESSURE_SOLVER` tag no longer present; original assignment preserved in this status file from earlier extraction.
- Follow-up correction: stress buffer order is now aligned to `BaseModule.GetActiveModuleAt()` / `_HectonModuleAmbienceDataBuffer` order, not graph rebuild order. Order hash resets transient spikes/hysteresis when active module order changes.
- Follow-up correction: low-tier flag changes force shader param re-upload even if stress is numerically stable.
- Follow-up correction: invalid module stress dump now uses a one-shot `Dump_VOLUMETRIC_PRESSURE_SOLVER.bin` guard instead of repeatedly rewriting the dump file.
- Follow-up scoped build filter: `dotnet build Hecton8.Core.csproj --no-restore -v:quiet -clp:ErrorsOnly | Select-String Habitat...` produced no touched-file errors; full build remains globally red.
- Follow-up correction: CPU stress upload is now clamped to the same 64-module capacity as `_HectonModuleAmbienceDataBuffer`; shader no-match radius lookup now returns a sentinel so off-module vertices read zero stress instead of slot 0.
- Serialized verification: `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:quiet -clp:ErrorsOnly` returned exit code 1 with 130 global error lines and 0 touched-file/contract error matches. First sampled failures remain missing cross-domain namespaces (`Hecton8.Environment.Fluids`, `Hecton8.Core.Scheduling`, `Hecton8.Audio.Virtualization`, etc.).
- Unity MCP verification after latest edits: refresh triggered but timed out waiting for editor readiness; console read still returned `no_unity_session`.
- Follow-up correction: order-change clears now skip redundant zero shader publication when the same tick will upload the replacement stress matrix; empty/dispose paths still clear shader state.
- Follow-up correction: active-order hashing now uses a runtime `BaseModule.GetInstanceID()` fallback when no graph record exists, preventing fallback slot-index hashes from hiding module reorder and migrating spikes/hysteresis.
- Latest verification: serialized `dotnet build` returned exit code 1 with 139 global error lines and 0 touched-file/contract matches. Unity console is reachable and reports unrelated `AudioVirtualizationJobs.cs(189,27)` / `(190,27)` `CS1615 ref` errors plus an entry-point exception.
