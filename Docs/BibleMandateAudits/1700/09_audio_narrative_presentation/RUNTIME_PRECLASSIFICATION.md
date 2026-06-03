# Runtime Preclassification - Audio, Narrative, PDA, Cinematics, Public Text

Status: HEURISTIC FIRST PASS - MANUAL REVIEW STILL REQUIRED
Date: 2026-06-02

This file groups static runtime suspects by a conservative heuristic. It can reduce review time, but it cannot prove a line is legal or illegal without reading the containing method and owner phase.

Total runtime suspects: 149.

## Summary

- LEGAL_EDITOR_OR_DEV_GUARDED: 74
- LIKELY_LEGAL_COLD_OR_OWNER_LIFETIME_PATH: 26
- REVIEW_RUNTIME_MESH_MATERIAL_PATH: 23
- REVIEW_NATIVE_LIFETIME_OR_ALLOCATOR_PROOF_REQUIRED: 18
- REVIEW_CACHE_OR_INJECTION_REQUIRED: 7
- LIKELY_LEGAL_COLD_OR_DIAGNOSTIC_PATH: 1

## LEGAL_EDITOR_OR_DEV_GUARDED (74)

- Runtime debug logging | Assets\_Project\Scripts\Audio\ProceduralAudioEvents.cs:1267:            Hecton8.Core.H8Debug.LogException(exception);
- Runtime debug logging | Assets\_Project\Scripts\Audio\HectonMusicDirector.cs:907:                    Hecton8.Core.H8Debug.LogError("[HectonMusicDirector] Missing authored HectonMusicDirectorConfig for active scene.");
- Runtime debug logging | Assets\_Project\Scripts\Audio\HectonMusicDirector.cs:917:                Hecton8.Core.H8Debug.LogError("[HectonMusicDirector] Missing authored RuntimeDirectorPrefab on active HectonMusicDirectorConfig.");
- Runtime debug logging | Assets\_Project\Scripts\Audio\AdaptiveStem\AdaptiveStemAudioMixer.cs:1315:                Hecton8.Core.H8Debug.LogWarning("[SHINOBU_46] Failed to dump adaptive stem telemetry.");
- Runtime debug logging | Assets\_Project\Scripts\Audio\AdaptiveStem\AdaptiveStemAudioMixer.cs:1319:                Hecton8.Core.H8Debug.LogWarning("[SHINOBU_46] Failed to dump adaptive stem telemetry.");
- Runtime debug logging | Assets\_Project\Scripts\Audio\AdaptiveStem\AdaptiveStemAudioMixer.cs:1372:                Hecton8.Core.H8Debug.LogWarning("[SHINOBU_46] audio_stem_rules.csv parse failed.");
- Runtime debug logging | Assets\_Project\Scripts\Audio\AdaptiveStem\AdaptiveStemAudioMixer.cs:1376:                Hecton8.Core.H8Debug.LogWarning("[SHINOBU_46] audio_stem_rules.csv parse failed.");
- Runtime debug logging | Assets\_Project\Scripts\Audio\PlayerCriticalProceduralAudioRenderer.cs:3493:            Hecton8.Core.H8Debug.LogWarning("[PlayerCriticalProceduralAudioRenderer] Audio producer thread failed to stop within watchdog budget. Native audio buffers remain owned until the worker exits.");
- Runtime debug logging | Assets\_Project\Scripts\Audio\PlayerCriticalProceduralAudioRenderer.cs:4514:                    Hecton8.Core.H8Debug.LogWarning("[PlayerCriticalProceduralAudioRenderer] Missing authored AudioReverbFilter. RequireComponent should install it before runtime; reverb fallback is disabled.", this);
- Runtime debug logging | Assets\_Project\Scripts\Audio\PlayerCriticalProceduralAudioRenderer.cs:4561:                    Hecton8.Core.H8Debug.LogWarning("[PlayerCriticalProceduralAudioRenderer] Reverb control mixer is missing one or more exposed parameters. Falling back to AudioReverbFilter.", this);
- Runtime debug logging | Assets\_Project\Scripts\Audio\PlayerCriticalProceduralAudioRenderer.cs:4582:                Hecton8.Core.H8Debug.LogWarning("[PlayerCriticalProceduralAudioRenderer] Reverb wet-mix parameter missing on AudioMixer. Decay/room parameters stay mixer-driven, wet mix falls back to the default mixer state.", this);
- Runtime debug logging | Assets\_Project\Scripts\Audio\PlayerCriticalProceduralAudioRenderer.cs:8763:                    Hecton8.Core.H8Debug.LogError(
- Runtime debug logging | Assets\_Project\Scripts\Audio\PlayerCriticalProceduralAudioRenderer.cs:8783:                Hecton8.Core.H8Debug.LogError(
- Runtime debug logging | Assets\_Project\Scripts\AudioLog\AudioLogSystem.cs:1681:            H8Debug.Log("[AudioLog] Playback completed.");
- Runtime debug logging | Assets\_Project\Scripts\AudioLog\AudioLogSystem.cs:1689:            H8Debug.Log("[AudioLog] Discovered.");
- Runtime debug logging | Assets\_Project\Scripts\AudioLog\AudioLogSystem.cs:1697:            H8Debug.Log("[AudioLog] Playing.");
- Runtime debug logging | Assets\_Project\Scripts\AudioLog\AudioLogSystem.cs:1705:            H8Debug.Log("[AudioLog] Loaded discovered logs.");
- Runtime debug logging | Assets\_Project\Scripts\AudioLog\AudioLogPickup.cs:241:                Hecton8.Core.H8Debug.LogWarning("[AudioLogPickup] No AudioLogData assigned.");
- Runtime debug logging | Assets\_Project\Scripts\AudioLog\AudioLogPickup.cs:250:                Hecton8.Core.H8Debug.LogWarning("[AudioLogPickup] AudioLogSystem service is not cached.");
- Runtime debug logging | Assets\_Project\Scripts\AudioLog\AudioLogEvents.cs:579:            Hecton8.Core.H8Debug.LogException(exception);
- Runtime debug logging | Assets\_Project\Scripts\Narrative\CorporateOrderSystem.cs:357:                    H8Debug.Log("[CorporateOrders] Conflict.");
- Runtime debug logging | Assets\_Project\Scripts\Narrative\CorporateOrderSystem.cs:363:            H8Debug.Log("[CorporateOrders] Delivered.");
- Runtime debug logging | Assets\_Project\Scripts\Narrative\ProceduralLoreDirector.cs:159:                H8Debug.LogWarning("[ProceduralLoreDirector] Installed director registry capacity exceeded; cold installer cannot prove duplicate state without component lookup.", this);
- Runtime debug logging | Assets\_Project\Scripts\Narrative\Campaign\MetaCampaignService.cs:1620:                    Hecton8.Core.H8Debug.LogError("MetaCampaignService blackbox native dump write failed.");
- Runtime debug logging | Assets\_Project\Scripts\Narrative\Campaign\MetaCampaignService.cs:1627:                Hecton8.Core.H8Debug.LogError("MetaCampaignService blackbox dump failed.");
- Runtime debug logging | Assets\_Project\Scripts\Narrative\LoreDatabaseManager.cs:101:                    Hecton8.Core.H8Debug.LogError("[LoreDatabaseManager] Spec hash mismatch.");
- Runtime debug logging | Assets\_Project\Scripts\Narrative\LoreDatabaseManager.cs:889:            Hecton8.Core.H8Debug.LogError("[LoreDatabaseManager] Duplicate lore hash.");
- Runtime debug logging | Assets\_Project\Scripts\Narrative\LoreDatabaseManager.cs:1041:                Hecton8.Core.H8Debug.LogError("[LoreDatabaseManager] Rebake failed. No authored lore seed source files were found.");
- Runtime debug logging | Assets\_Project\Scripts\Narrative\LoreDatabaseManager.cs:1076:                Hecton8.Core.H8Debug.Log("[LoreDatabaseManager] Lore seed hashes already match the runtime ASCII FNV-1a owner across authored source files.");
- Runtime debug logging | Assets\_Project\Scripts\Narrative\LoreDatabaseManager.cs:1080:            Hecton8.Core.H8Debug.Log("[LoreDatabaseManager] Rebaked lore seed hashes.");
- Runtime debug logging | Assets\_Project\Scripts\Quest\QuestEvents.cs:506:            Hecton8.Core.H8Debug.LogException(exception);
- Runtime debug logging | Assets\_Project\Scripts\Quest\QuestManager.cs:575:                Hecton8.Core.H8Debug.LogError("[QuestManager] Quest registry ambiguity detected.");
- Runtime debug logging | Assets\_Project\Scripts\Quest\QuestManager.cs:584:                Hecton8.Core.H8Debug.LogError("[QuestManager] Quest state graph compilation failed.");
- Runtime debug logging | Assets\_Project\Scripts\Quest\QuestManager.cs:983:                    Hecton8.Core.H8Debug.LogWarning("[QuestManager] Unknown questId.");
- Runtime debug logging | Assets\_Project\Scripts\PDA\PDALogbookManager.cs:488:                Hecton8.Core.H8Debug.LogError("[PDALogbookManager] Duplicate logbook service detected. Disabling duplicate.");
- Runtime debug logging | Assets\_Project\Scripts\VFX\BiomeProfile.cs:68:                Hecton8.Core.H8Debug.LogWarning("[BiomeProfile] High AOIntensity may impact performance.");
- Runtime debug logging | Assets\_Project\Scripts\VFX\CameraJuiceSystem_CameraJuiceBurst.cs:184:                Hecton8.Core.H8Debug.LogError("[SHINOBU_354] Camera juice ABI violation.");
- Runtime debug logging | Assets\_Project\Scripts\VFX\CameraJuiceSystem.cs:861:            Hecton8.Core.H8Debug.LogError("[CameraJuiceSystem] Duplicate instance detected. Destroying duplicate.");
- Runtime debug logging | Assets\_Project\Scripts\VFX\CameraJuiceSystem.cs:869:            Hecton8.Core.H8Debug.LogError("[CameraJuiceSystem] MainCamera not found. System disabled.");
- Runtime debug logging | Assets\_Project\Scripts\VFX\CameraJuiceSystem.cs:877:            Hecton8.Core.H8Debug.LogError("[CameraJuiceSystem] URPVolume not found. Post-processing disabled.");
- Additional lines omitted here: 34. Use `../_scans/09_audio_narrative_presentation_runtime_risks.txt` for the full list.

## LIKELY_LEGAL_COLD_OR_OWNER_LIFETIME_PATH (26)

- Native allocation or persistent lifetime | Assets\_Project\Scripts\Audio\Synthesis\VocalBankPlaybackRuntime.cs:1355:                _editorCsvScratch = (byte*)UnsafeUtility.Malloc(EditorCsvScratchBytes, 16, Allocator.Persistent);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Audio\Synthesis\VocalBankPlaybackRuntime.cs:1367:                UnsafeUtility.Free(_editorCsvScratch, Allocator.Persistent);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Narrative\Prologue\AwaitableDropSequenceDirector.cs:1227:                payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Narrative\Campaign\MetaCampaignService.cs:1591:                payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Quest\QuestDagResolverRuntime.cs:595:                Allocator.Persistent); // COLD ALLOC: NativeParallelMultiHashMap<int,int>[triggerCapacity*27] - expanded trigger-cell occupancy, quest truth remains in GlobalDataVault - owner: QuestDagResolverService
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Quest\QuestStateManager.cs:415:            _prerequisites = new NativeArray<QuestPrerequisiteDescriptor>(prerequisiteBuilder.Count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\PDA\CartographyGridJobs.cs:1155:                payload = new NativeArray<byte>(byteCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\VFX\Debris\ShinobuVoxelSculptorWindow.cs:248:                densities = new NativeArray<sbyte>(GridCellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\VFX\Debris\ShinobuVoxelSculptorWindow.cs:249:                decompressed = new NativeArray<sbyte>(GridCellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\VFX\Debris\ShinobuVoxelSculptorWindow.cs:250:                stats = new NativeArray<int>(StatsLength, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\VFX\Debris\ShinobuVoxelSculptorWindow.cs:251:                writtenCount = new NativeArray<int>(CounterLength, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\VFX\Debris\ShinobuVoxelSculptorWindow.cs:679:            densities = new NativeArray<sbyte>(GridCellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\VFX\Debris\ShinobuVoxelSculptorWindow.cs:680:            decompressed = new NativeArray<sbyte>(GridCellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\VFX\Debris\ShinobuVoxelSculptorWindow.cs:681:            accumulator = new NativeArray<int>(GridCellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\VFX\Debris\ShinobuVoxelSculptorWindow.cs:682:            removedMass = new NativeArray<int>(CounterLength, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\VFX\Debris\ShinobuVoxelSculptorWindow.cs:683:            debrisCount = new NativeArray<int>(CounterLength, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\VFX\Debris\ShinobuVoxelSculptorWindow.cs:684:            stats = new NativeArray<int>(StatsLength, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\VFX\Debris\ShinobuVoxelSculptorWindow.cs:685:            writtenCount = new NativeArray<int>(CounterLength, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\VFX\Debris\ShinobuVoxelSculptorWindow.cs:686:            particles = new NativeArray<DebrisParticleDTO>(ShinobuDeltaCrusher.MaximumQualityDebrisCap, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Visor\HectonVisorUberPostFeature.cs:1797:                payload = new NativeArray<byte>(totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Visor\HectonVisorARStencilRendererFeature.cs:1378:                payload = new NativeArray<byte>(totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Visor\HectonVisorFluidDistortionFeature.cs:1731:                payload = new NativeArray<byte>(totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Visor\HectonVisorUberPostFeature.Noir.cs:1076:                payload = new NativeArray<byte>(totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Visor\HectonVolumetricParticulateFogFeature.cs:1963:                    payload = new NativeArray<byte>(totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Visor\InternalFloodWaterlineRuntime.cs:789:                payload = new NativeArray<byte>(totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Visor\SpectrumSystem.cs:3762:                payload = new NativeArray<byte>(totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

## REVIEW_RUNTIME_MESH_MATERIAL_PATH (23)

- Runtime mesh/material mutation | Assets\_Project\Scripts\Quest\MissionMarkerSystem.cs:705:            mesh.RecalculateNormals();
- Runtime mesh/material mutation | Assets\_Project\Scripts\VFX\Debris\CarveDebrisComputeRenderer.cs:2393:            mesh.RecalculateNormals();
- Runtime mesh/material mutation | Assets\_Project\Scripts\Visor\HectonBiolumSSGIFeature.cs:353:                    passData.material = _compositeMaterial;
- Runtime mesh/material mutation | Assets\_Project\Scripts\Visor\HectonBiolumSSGIFeature.cs:365:                        if (data.material == null)
- Runtime mesh/material mutation | Assets\_Project\Scripts\Visor\HectonBiolumSSGIFeature.cs:370:                        CoreUtils.DrawFullScreen(context.cmd, data.material, null, data.shaderPassIndex);
- Runtime mesh/material mutation | Assets\_Project\Scripts\Visor\HectonBiolumSSGIFeature.cs:398:                    passData.material = _compositeMaterial;
- Runtime mesh/material mutation | Assets\_Project\Scripts\Visor\HectonBiolumSSGIFeature.cs:415:                        if (data.material == null)
- Runtime mesh/material mutation | Assets\_Project\Scripts\Visor\HectonBiolumSSGIFeature.cs:427:                        CoreUtils.DrawFullScreen(cmd, data.material, null, 1);
- Runtime mesh/material mutation | Assets\_Project\Scripts\Visor\HectonHolographicEdgeFeature.cs:94:                    passData.material = _material;
- Runtime mesh/material mutation | Assets\_Project\Scripts\Visor\HectonHolographicEdgeFeature.cs:103:                        HectonScanRenderRegistry.DrawRenderers(context.cmd, data.material, data.requiredFlags, data.maxDrawnTargets);
- Runtime mesh/material mutation | Assets\_Project\Scripts\Visor\HectonScooterVolumetricShaftsFeature.cs:639:                    passData.material = material;
- Runtime mesh/material mutation | Assets\_Project\Scripts\Visor\HectonScooterVolumetricShaftsFeature.cs:661:                        if (data.material == null)
- Runtime mesh/material mutation | Assets\_Project\Scripts\Visor\HectonScooterVolumetricShaftsFeature.cs:687:                        CoreUtils.DrawFullScreen(context.cmd, data.material, null, data.shaderPassIndex);
- Runtime mesh/material mutation | Assets\_Project\Scripts\Visor\HectonVolumetricParticulateFogFeature.cs:1111:                    passData.material = _dearLieProxyMaterial;
- Runtime mesh/material mutation | Assets\_Project\Scripts\Visor\HectonVolumetricParticulateFogFeature.cs:1128:                        if (data.material == null ||
- Runtime mesh/material mutation | Assets\_Project\Scripts\Visor\HectonVolumetricParticulateFogFeature.cs:1149:                        CoreUtils.DrawFullScreen(context.cmd, data.material, null, data.passIndex);
- Runtime mesh/material mutation | Assets\_Project\Scripts\Visor\HectonSonarPointCloudFeature.cs:311:                    passData.material = material;
- Runtime mesh/material mutation | Assets\_Project\Scripts\Visor\HectonSonarPointCloudFeature.cs:329:                        if (data.material == null)
- Runtime mesh/material mutation | Assets\_Project\Scripts\Visor\HectonSonarPointCloudFeature.cs:345:                        CoreUtils.DrawFullScreen(context.cmd, data.material, null, data.shaderPassIndex);
- Runtime mesh/material mutation | Assets\_Project\Scripts\Visor\VisorHUDController.cs:2151:            return font != null ? font.material : null;
- Runtime mesh/material mutation | Assets\_Project\Scripts\Visor\VolumetricLightFeature.cs:563:                    passData.material = _proxyMaterial;
- Runtime mesh/material mutation | Assets\_Project\Scripts\Visor\VolumetricLightFeature.cs:586:                        if (data.material == null)
- Runtime mesh/material mutation | Assets\_Project\Scripts\Visor\VolumetricLightFeature.cs:605:                        CoreUtils.DrawFullScreen(context.cmd, data.material, null, 0);

## REVIEW_NATIVE_LIFETIME_OR_ALLOCATOR_PROOF_REQUIRED (18)

- Native allocation or persistent lifetime | Assets\_Project\Scripts\Audio\NativeAudioFrameRingBuffer.cs:506:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Audio\NativeAudioFrameRingBuffer.cs:515:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Audio\NativeAudioFrameRingBuffer.cs:527:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Audio\NativeAudioFrameRingBuffer.cs:539:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Audio\NativeAudioFrameRingBuffer.cs:565:                H8Memory.FreeRaw(_telemetryDumpBytesPtr, Allocator.Persistent, VaultOwner);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Audio\NativeAudioFrameRingBuffer.cs:571:                H8Memory.FreeRaw(_telemetryPtr, Allocator.Persistent, VaultOwner);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Audio\NativeAudioFrameRingBuffer.cs:577:                H8Memory.FreeRaw(_sharedStatePtr, Allocator.Persistent, VaultOwner);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Audio\NativeAudioFrameRingBuffer.cs:583:                H8Memory.FreeRaw(_framesPtr, Allocator.Persistent, VaultOwner);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\AudioLog\AudioLogEvents.cs:80:        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Quest\QuestGraphEvaluator.cs:24:        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Quest\QuestStateManager.cs:46:        private const Allocator DataVaultExemptQuestStateAllocator = Allocator.Persistent;
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Quest\QuestStateManager.cs:194:            _globalPrerequisites = new NativeArray<uint>(WordCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Quest\QuestStateManager.cs:410:            _nodes = new NativeArray<QuestNodeDescriptor>(nodeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Quest\QuestEvents.cs:59:        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
- Native allocation or persistent lifetime | Assets\_Project\Scripts\VFX\PlasmaBeam\ShinobuPlasmaBeamRuntime.cs:1486:                payload = new NativeArray<byte>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\VFX\Bioluminescence\BiolumPulseSyncRuntime.cs:316:                    entries = new NativeArray<BiolumPulseTelemetryEntry>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\VFX\Bioluminescence\BiolumPulseSyncRuntime.cs:318:                        Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Visor\DynamicDecalVaultRuntime.cs:2339:                NativeArray<byte> payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.ClearMemory);

## REVIEW_CACHE_OR_INJECTION_REQUIRED (7)

- Unity scene lookup | Assets\_Project\Scripts\Audio\Synthesis\VocalBankPlaybackRuntime.cs:306:            bool hasListener = TryGetComponent<AudioListener>(out _);
- Unity scene lookup | Assets\_Project\Scripts\Audio\Synthesis\DynamicMusic\DynamicMusicGranularSynthesizer.cs:681:            if (!TryGetComponent<AudioListener>(out _))
- Unity scene lookup | Assets\_Project\Scripts\PDA\PDARuntimeInstaller.cs:20:            if (!playerObject.TryGetComponent<IPlayerExplorationChunkReadModel>(out _))
- Unity scene lookup | Assets\_Project\Scripts\PDA\PDARuntimeInstaller.cs:23:            if (!playerObject.TryGetComponent<PDALogbookManager>(out _))
- Unity scene lookup | Assets\_Project\Scripts\PDA\PDARuntimeInstaller.cs:26:            if (!playerObject.TryGetComponent<PDAMarkerRegistry>(out _))
- Unity scene lookup | Assets\_Project\Scripts\PDA\PDARuntimeInstaller.cs:29:            if (!playerObject.TryGetComponent<PDAIntrusionManager>(out _))
- Unity scene lookup | Assets\_Project\Scripts\PDA\PDAMarkerHUDElement.cs:573:            root.GetComponentsInChildren(true, s_GraphicRaycastDisableScratch);

## LIKELY_LEGAL_COLD_OR_DIAGNOSTIC_PATH (1)

- Runtime debug logging | Assets\_Project\Scripts\Quest\NarrativeDagInspectorWindow.cs:62:                            Debug.LogError("Narrative DAG buffers were not initialized because the DataVault is unavailable or fenced.");

