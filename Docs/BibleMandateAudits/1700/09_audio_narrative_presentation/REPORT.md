# Audio, Narrative, PDA, Cinematics, Public Text

Status: STATIC BIBLE/MANDATE/CODEBASE AUDIT - RUNTIME PROOF NOT RUN
Date: 2026-06-02
Verdict: YELLOW_RUNTIME_STATIC_RISK_REVIEW_REQUIRED

## Scope

This report compares the current root bible routes and selected mandate registry files against static codebase evidence. It does not prove Unity import health, Play Mode behavior, profiler cost, memory use, visual quality, or device performance.

## Bibles Checked

- OK audio.md - 168 lines; GlobalQualityWeight, proof, acceptance, rejection.
- OK narrative.md - 138 lines; GlobalQualityWeight, proof, acceptance, rejection.
- OK presentation.md - 160 lines; GlobalQualityWeight, proof, acceptance, rejection.
- OK cinematics.md - 102 lines; GlobalQualityWeight, proof, acceptance, rejection.
- OK textes.md - 669 lines; GlobalQualityWeight, proof, acceptance, rejection.
- OK accessibility.md - 129 lines; GlobalQualityWeight, proof, acceptance, rejection.
- OK sonar.md - 121 lines; GlobalQualityWeight, proof, acceptance, rejection.

## Mandates Matched

- .agents-skills\AUD_Acoustic_Sonar_Occlusion_Sensory_Simulation.txt
- .agents-skills\AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC.txt
- .agents-skills\AUDIO_Hrtf_Binaural_Spatialization.txt
- .agents-skills\OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt
- .agents-skills\PROG_Quest_State_Graph_Logic.txt
- .agents-skills\QA_Evidence_Text_Filter_Audit.txt

## Code/Asset Roots

- OK Assets\_Project\Scripts\Audio
- OK Assets\_Project\Scripts\AudioLog
- OK Assets\_Project\Scripts\Narrative
- OK Assets\_Project\Scripts\Quest
- OK Assets\_Project\Scripts\PDA
- OK Assets\_Project\Scripts\VFX
- OK Assets\_Project\Scripts\Visor

## Static Evidence Found

Total matching files: 161. Showing first 80. Full list: _scans/09_audio_narrative_presentation_evidence_files.txt.

- Assets\_Project\Scripts\Audio\AcousticPortalPropagation.cs
- Assets\_Project\Scripts\Audio\AcousticReverbPresetTrigger.cs
- Assets\_Project\Scripts\Audio\AdaptiveStem\AdaptiveStemAudioMixer.cs
- Assets\_Project\Scripts\Audio\AtmosphericAudioRuntimeInstaller.cs
- Assets\_Project\Scripts\Audio\AudioMaterialProfile.cs
- Assets\_Project\Scripts\Audio\AudioVirtualizationJobs.cs
- Assets\_Project\Scripts\Audio\DeepPsychosisController.cs
- Assets\_Project\Scripts\Audio\Echolocation\AcousticEcholocationRaymarch.cs
- Assets\_Project\Scripts\Audio\Echolocation\Hecton8.Audio.Echolocation.asmdef
- Assets\_Project\Scripts\Audio\Editor\AbyssalAcousticsTunerWindow.cs
- Assets\_Project\Scripts\Audio\Editor\AbyssalDspTunerWindow.cs
- Assets\_Project\Scripts\Audio\Editor\AcousticPortalMemorySovereigntyValidator.cs
- Assets\_Project\Scripts\Audio\Editor\AdaptiveAudioTunerWindow.cs
- Assets\_Project\Scripts\Audio\Editor\AdvancedAcousticsSmokeTester.cs
- Assets\_Project\Scripts\Audio\Editor\AudioImportDictator.cs
- Assets\_Project\Scripts\Audio\Editor\AudioMemorySovereigntyValidator1320.cs
- Assets\_Project\Scripts\Audio\Editor\AudioOmegaAutonomySmokeTester.cs
- Assets\_Project\Scripts\Audio\Editor\DSPThreadSafetySmokeTester.cs
- Assets\_Project\Scripts\Audio\Editor\GranularSynthTunerWindow.cs
- Assets\_Project\Scripts\Audio\Editor\OOP_AudioSource_Scanner.cs
- Assets\_Project\Scripts\Audio\Editor\OOP_Voice_Scanner_SHINOBU_352.cs
- Assets\_Project\Scripts\Audio\Editor\OOP_Voice_Scanner_X_011.cs
- Assets\_Project\Scripts\Audio\Editor\SabineReverbDspTunerWindow.cs
- Assets\_Project\Scripts\Audio\Editor\Shinobu351HullStressDspSmokeTester.cs
- Assets\_Project\Scripts\Audio\Editor\ShinobuAcousticDspSmokeTester.cs
- Assets\_Project\Scripts\Audio\Editor\VocalWarningAlarmBitmaskAudit_1629.cs
- Assets\_Project\Scripts\Audio\Editor\VocalWarningQueueDebugGizmo.cs
- Assets\_Project\Scripts\Audio\Editor\VocalWarningQueueTunerWindow.cs
- Assets\_Project\Scripts\Audio\Editor\VocalWarningStormTorture_X_011.cs
- Assets\_Project\Scripts\Audio\HectonMusicBiomeProfile.cs
- Assets\_Project\Scripts\Audio\HectonMusicClip.cs
- Assets\_Project\Scripts\Audio\HectonMusicDirector.cs
- Assets\_Project\Scripts\Audio\HectonMusicDirectorAnchor.cs
- Assets\_Project\Scripts\Audio\HectonMusicDirectorConfig.cs
- Assets\_Project\Scripts\Audio\HectonSensoryKernelNativeBridge.cs
- Assets\_Project\Scripts\Audio\MusicVoicePool.cs
- Assets\_Project\Scripts\Audio\NativeAudioFrameRingBuffer.cs
- Assets\_Project\Scripts\Audio\PlayerCriticalBufferJobs.cs
- Assets\_Project\Scripts\Audio\PlayerCriticalMetallicGrainBank.cs
- Assets\_Project\Scripts\Audio\PlayerCriticalProceduralAudioRenderer.cs
- Assets\_Project\Scripts\Audio\ProceduralAudioEvents.cs
- Assets\_Project\Scripts\Audio\Prologue\Hecton8.Audio.Prologue.asmdef
- Assets\_Project\Scripts\Audio\Prologue\PrologueAcousticOrchestrator.cs
- Assets\_Project\Scripts\Audio\Synthesis\DepthStressGranularSynthesisKernel.cs
- Assets\_Project\Scripts\Audio\Synthesis\DynamicMusic\DynamicMusicGranularSynthesizer.cs
- Assets\_Project\Scripts\Audio\Synthesis\Editor\AbyssalSynthTunerWindow.cs
- Assets\_Project\Scripts\Audio\Synthesis\Editor\AudioSynthesisMemorySovereigntyValidator.cs
- Assets\_Project\Scripts\Audio\Synthesis\Editor\DigitalVoiceForgeWindow.cs
- Assets\_Project\Scripts\Audio\Synthesis\Editor\Hecton8.Audio.Synthesis.Editor.asmdef
- Assets\_Project\Scripts\Audio\Synthesis\Editor\VocalStateLayoutValidator.cs
- Assets\_Project\Scripts\Audio\Synthesis\Hecton8.Audio.Synthesis.asmdef
- Assets\_Project\Scripts\Audio\Synthesis\HullStressGranularDspKernel.cs
- Assets\_Project\Scripts\Audio\Synthesis\ShinobuDiegeticGlitchSynthBridge.cs
- Assets\_Project\Scripts\Audio\Synthesis\VocalBankContracts.cs
- Assets\_Project\Scripts\Audio\Synthesis\VocalBankPlaybackRuntime.cs
- Assets\_Project\Scripts\Audio\Virtualization\Contracts\AudioVirtualizationContracts.cs
- Assets\_Project\Scripts\Audio\Virtualization\Contracts\Hecton8.Audio.Virtualization.Contracts.asmdef
- Assets\_Project\Scripts\Audio\VocalWarningSystem.cs
- Assets\_Project\Scripts\AudioLog\AudioLogData.cs
- Assets\_Project\Scripts\AudioLog\AudioLogDiscoveryBitMask.cs
- Assets\_Project\Scripts\AudioLog\AudioLogEvents.cs
- Assets\_Project\Scripts\AudioLog\AudioLogPickup.cs
- Assets\_Project\Scripts\AudioLog\AudioLogSystem.cs
- Assets\_Project\Scripts\Narrative\Camera\CinematicMath.cs
- Assets\_Project\Scripts\Narrative\Camera\Hecton8.Narrative.Camera.asmdef
- Assets\_Project\Scripts\Narrative\Campaign\Hecton8.Narrative.Campaign.asmdef
- Assets\_Project\Scripts\Narrative\Campaign\MetaCampaignService.cs
- Assets\_Project\Scripts\Narrative\ColonistLoreRegistry.cs
- Assets\_Project\Scripts\Narrative\CorporateOrderSystem.cs
- Assets\_Project\Scripts\Narrative\DeepReachCorporationData.cs
- Assets\_Project\Scripts\Narrative\FaunaLoreRegistry.cs
- Assets\_Project\Scripts\Narrative\HectonNarrativeDirector_PoiTriggers.cs
- Assets\_Project\Scripts\Narrative\LoreDatabaseManager.cs
- Assets\_Project\Scripts\Narrative\LoreEncyclopediaLazyProxy.cs
- Assets\_Project\Scripts\Narrative\LoreMmfEncyclopedia.cs
- Assets\_Project\Scripts\Narrative\NarrativeRuntimeInstaller.cs
- Assets\_Project\Scripts\Narrative\ProceduralLoreDirector.cs
- Assets\_Project\Scripts\Narrative\Prologue\AwaitableDropSequenceDirector.cs
- Assets\_Project\Scripts\Narrative\Prologue\Hecton8.Narrative.Prologue.asmdef
- Assets\_Project\Scripts\Narrative\Prologue\ReentrySequenceMetricValidator1603.cs

## Static Risk Suspects

These are suspects, not confirmed defects. Runtime suspects need code review. Editor/tool suspects are legal only if they cannot execute in gameplay/player hot paths.

Runtime suspects:
Total runtime suspects: 149. Showing first 80. Full list: _scans/09_audio_narrative_presentation_runtime_risks.txt.

- Assets\_Project\Scripts\Audio\AdaptiveStem\AdaptiveStemAudioMixer.cs:1315:                Hecton8.Core.H8Debug.LogWarning("[SHINOBU_46] Failed to dump adaptive stem telemetry.");
- Assets\_Project\Scripts\Audio\AdaptiveStem\AdaptiveStemAudioMixer.cs:1319:                Hecton8.Core.H8Debug.LogWarning("[SHINOBU_46] Failed to dump adaptive stem telemetry.");
- Assets\_Project\Scripts\Audio\AdaptiveStem\AdaptiveStemAudioMixer.cs:1372:                Hecton8.Core.H8Debug.LogWarning("[SHINOBU_46] audio_stem_rules.csv parse failed.");
- Assets\_Project\Scripts\Audio\AdaptiveStem\AdaptiveStemAudioMixer.cs:1376:                Hecton8.Core.H8Debug.LogWarning("[SHINOBU_46] audio_stem_rules.csv parse failed.");
- Assets\_Project\Scripts\Audio\NativeAudioFrameRingBuffer.cs:506:                Allocator.Persistent,
- Assets\_Project\Scripts\Audio\NativeAudioFrameRingBuffer.cs:515:                Allocator.Persistent,
- Assets\_Project\Scripts\Audio\NativeAudioFrameRingBuffer.cs:527:                Allocator.Persistent,
- Assets\_Project\Scripts\Audio\NativeAudioFrameRingBuffer.cs:539:                Allocator.Persistent,
- Assets\_Project\Scripts\Audio\NativeAudioFrameRingBuffer.cs:565:                H8Memory.FreeRaw(_telemetryDumpBytesPtr, Allocator.Persistent, VaultOwner);
- Assets\_Project\Scripts\Audio\NativeAudioFrameRingBuffer.cs:571:                H8Memory.FreeRaw(_telemetryPtr, Allocator.Persistent, VaultOwner);
- Assets\_Project\Scripts\Audio\NativeAudioFrameRingBuffer.cs:577:                H8Memory.FreeRaw(_sharedStatePtr, Allocator.Persistent, VaultOwner);
- Assets\_Project\Scripts\Audio\NativeAudioFrameRingBuffer.cs:583:                H8Memory.FreeRaw(_framesPtr, Allocator.Persistent, VaultOwner);
- Assets\_Project\Scripts\Audio\ProceduralAudioEvents.cs:1267:            Hecton8.Core.H8Debug.LogException(exception);
- Assets\_Project\Scripts\Audio\PlayerCriticalProceduralAudioRenderer.cs:3493:            Hecton8.Core.H8Debug.LogWarning("[PlayerCriticalProceduralAudioRenderer] Audio producer thread failed to stop within watchdog budget. Native audio buffers remain owned until the worker exits.");
- Assets\_Project\Scripts\Audio\PlayerCriticalProceduralAudioRenderer.cs:4514:                    Hecton8.Core.H8Debug.LogWarning("[PlayerCriticalProceduralAudioRenderer] Missing authored AudioReverbFilter. RequireComponent should install it before runtime; reverb fallback is disabled.", this);
- Assets\_Project\Scripts\Audio\PlayerCriticalProceduralAudioRenderer.cs:4561:                    Hecton8.Core.H8Debug.LogWarning("[PlayerCriticalProceduralAudioRenderer] Reverb control mixer is missing one or more exposed parameters. Falling back to AudioReverbFilter.", this);
- Assets\_Project\Scripts\Audio\PlayerCriticalProceduralAudioRenderer.cs:4582:                Hecton8.Core.H8Debug.LogWarning("[PlayerCriticalProceduralAudioRenderer] Reverb wet-mix parameter missing on AudioMixer. Decay/room parameters stay mixer-driven, wet mix falls back to the default mixer state.", this);
- Assets\_Project\Scripts\Audio\PlayerCriticalProceduralAudioRenderer.cs:8763:                    Hecton8.Core.H8Debug.LogError(
- Assets\_Project\Scripts\Audio\PlayerCriticalProceduralAudioRenderer.cs:8783:                Hecton8.Core.H8Debug.LogError(
- Assets\_Project\Scripts\Audio\Synthesis\DynamicMusic\DynamicMusicGranularSynthesizer.cs:681:            if (!TryGetComponent<AudioListener>(out _))
- Assets\_Project\Scripts\Audio\HectonMusicDirector.cs:907:                    Hecton8.Core.H8Debug.LogError("[HectonMusicDirector] Missing authored HectonMusicDirectorConfig for active scene.");
- Assets\_Project\Scripts\Audio\HectonMusicDirector.cs:917:                Hecton8.Core.H8Debug.LogError("[HectonMusicDirector] Missing authored RuntimeDirectorPrefab on active HectonMusicDirectorConfig.");
- Assets\_Project\Scripts\Audio\Synthesis\VocalBankPlaybackRuntime.cs:306:            bool hasListener = TryGetComponent<AudioListener>(out _);
- Assets\_Project\Scripts\Audio\Synthesis\VocalBankPlaybackRuntime.cs:1355:                _editorCsvScratch = (byte*)UnsafeUtility.Malloc(EditorCsvScratchBytes, 16, Allocator.Persistent);
- Assets\_Project\Scripts\Audio\Synthesis\VocalBankPlaybackRuntime.cs:1367:                UnsafeUtility.Free(_editorCsvScratch, Allocator.Persistent);
- Assets\_Project\Scripts\AudioLog\AudioLogPickup.cs:241:                Hecton8.Core.H8Debug.LogWarning("[AudioLogPickup] No AudioLogData assigned.");
- Assets\_Project\Scripts\AudioLog\AudioLogPickup.cs:250:                Hecton8.Core.H8Debug.LogWarning("[AudioLogPickup] AudioLogSystem service is not cached.");
- Assets\_Project\Scripts\AudioLog\AudioLogSystem.cs:1681:            H8Debug.Log("[AudioLog] Playback completed.");
- Assets\_Project\Scripts\AudioLog\AudioLogSystem.cs:1689:            H8Debug.Log("[AudioLog] Discovered.");
- Assets\_Project\Scripts\AudioLog\AudioLogSystem.cs:1697:            H8Debug.Log("[AudioLog] Playing.");
- Assets\_Project\Scripts\AudioLog\AudioLogSystem.cs:1705:            H8Debug.Log("[AudioLog] Loaded discovered logs.");
- Assets\_Project\Scripts\AudioLog\AudioLogEvents.cs:80:        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
- Assets\_Project\Scripts\AudioLog\AudioLogEvents.cs:579:            Hecton8.Core.H8Debug.LogException(exception);
- Assets\_Project\Scripts\Narrative\CorporateOrderSystem.cs:357:                    H8Debug.Log("[CorporateOrders] Conflict.");
- Assets\_Project\Scripts\Narrative\CorporateOrderSystem.cs:363:            H8Debug.Log("[CorporateOrders] Delivered.");
- Assets\_Project\Scripts\Narrative\Campaign\MetaCampaignService.cs:1591:                payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\Narrative\Campaign\MetaCampaignService.cs:1620:                    Hecton8.Core.H8Debug.LogError("MetaCampaignService blackbox native dump write failed.");
- Assets\_Project\Scripts\Narrative\Campaign\MetaCampaignService.cs:1627:                Hecton8.Core.H8Debug.LogError("MetaCampaignService blackbox dump failed.");
- Assets\_Project\Scripts\Narrative\ProceduralLoreDirector.cs:159:                H8Debug.LogWarning("[ProceduralLoreDirector] Installed director registry capacity exceeded; cold installer cannot prove duplicate state without component lookup.", this);
- Assets\_Project\Scripts\Narrative\Prologue\AwaitableDropSequenceDirector.cs:1226:                payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\Narrative\LoreDatabaseManager.cs:101:                    Hecton8.Core.H8Debug.LogError("[LoreDatabaseManager] Spec hash mismatch.");
- Assets\_Project\Scripts\Narrative\LoreDatabaseManager.cs:889:            Hecton8.Core.H8Debug.LogError("[LoreDatabaseManager] Duplicate lore hash.");
- Assets\_Project\Scripts\Narrative\LoreDatabaseManager.cs:1041:                Hecton8.Core.H8Debug.LogError("[LoreDatabaseManager] Rebake failed. No authored lore seed source files were found.");
- Assets\_Project\Scripts\Narrative\LoreDatabaseManager.cs:1076:                Hecton8.Core.H8Debug.Log("[LoreDatabaseManager] Lore seed hashes already match the runtime ASCII FNV-1a owner across authored source files.");
- Assets\_Project\Scripts\Narrative\LoreDatabaseManager.cs:1080:            Hecton8.Core.H8Debug.Log("[LoreDatabaseManager] Rebaked lore seed hashes.");
- Assets\_Project\Scripts\Quest\QuestGraphEvaluator.cs:24:        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
- Assets\_Project\Scripts\Quest\MissionMarkerSystem.cs:705:            mesh.RecalculateNormals();
- Assets\_Project\Scripts\Quest\QuestManager.cs:525:                Hecton8.Core.H8Debug.LogError("[QuestManager] Quest registry ambiguity detected.");
- Assets\_Project\Scripts\Quest\QuestManager.cs:534:                Hecton8.Core.H8Debug.LogError("[QuestManager] Quest state graph compilation failed.");
- Assets\_Project\Scripts\Quest\QuestManager.cs:605:                    Hecton8.Core.H8Debug.LogWarning("[QuestManager] Unknown questId.");
- Assets\_Project\Scripts\Quest\QuestEvents.cs:59:        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
- Assets\_Project\Scripts\Quest\QuestEvents.cs:506:            Hecton8.Core.H8Debug.LogException(exception);
- Assets\_Project\Scripts\Quest\QuestDagResolverRuntime.cs:569:                Allocator.Persistent); // COLD ALLOC: NativeParallelMultiHashMap<int,int>[triggerCapacity*27] - expanded trigger-cell occupancy, quest truth remains in GlobalDataVault - owner: QuestDagResolverService
- Assets\_Project\Scripts\Quest\NarrativeDagInspectorWindow.cs:62:                            Debug.LogError("Narrative DAG buffers were not initialized because the DataVault is unavailable or fenced.");
- Assets\_Project\Scripts\Quest\QuestStateManager.cs:46:        private const Allocator DataVaultExemptQuestStateAllocator = Allocator.Persistent;
- Assets\_Project\Scripts\Quest\QuestStateManager.cs:194:            _globalPrerequisites = new NativeArray<uint>(WordCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Quest\QuestStateManager.cs:410:            _nodes = new NativeArray<QuestNodeDescriptor>(nodeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Quest\QuestStateManager.cs:415:            _prerequisites = new NativeArray<QuestPrerequisiteDescriptor>(prerequisiteBuilder.Count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\PDA\PDARuntimeInstaller.cs:20:            if (!playerObject.TryGetComponent<IPlayerExplorationChunkReadModel>(out _))
- Assets\_Project\Scripts\PDA\PDARuntimeInstaller.cs:23:            if (!playerObject.TryGetComponent<PDALogbookManager>(out _))
- Assets\_Project\Scripts\PDA\PDARuntimeInstaller.cs:26:            if (!playerObject.TryGetComponent<PDAMarkerRegistry>(out _))
- Assets\_Project\Scripts\PDA\PDARuntimeInstaller.cs:29:            if (!playerObject.TryGetComponent<PDAIntrusionManager>(out _))
- Assets\_Project\Scripts\PDA\PDALogbookManager.cs:488:                Hecton8.Core.H8Debug.LogError("[PDALogbookManager] Duplicate logbook service detected. Disabling duplicate.");
- Assets\_Project\Scripts\PDA\PDAMarkerHUDElement.cs:573:            root.GetComponentsInChildren(true, s_GraphicRaycastDisableScratch);
- Assets\_Project\Scripts\PDA\CartographyGridJobs.cs:1155:                payload = new NativeArray<byte>(byteCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\VFX\BiomeProfile.cs:68:                Hecton8.Core.H8Debug.LogWarning("[BiomeProfile] High AOIntensity may impact performance.");
- Assets\_Project\Scripts\VFX\CameraJuiceSystem_CameraJuiceBurst.cs:184:                Hecton8.Core.H8Debug.LogError("[SHINOBU_354] Camera juice ABI violation.");
- Assets\_Project\Scripts\VFX\ShakeProfile.cs:52:                Hecton8.Core.H8Debug.LogWarning("[ShakeProfile] Invalid Duration. Clamping to 0.5s.");
- Assets\_Project\Scripts\VFX\Debris\ShinobuVoxelSculptorWindow.cs:248:                densities = new NativeArray<sbyte>(GridCellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\VFX\Debris\ShinobuVoxelSculptorWindow.cs:249:                decompressed = new NativeArray<sbyte>(GridCellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\VFX\Debris\ShinobuVoxelSculptorWindow.cs:250:                stats = new NativeArray<int>(StatsLength, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\VFX\Debris\ShinobuVoxelSculptorWindow.cs:251:                writtenCount = new NativeArray<int>(CounterLength, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\VFX\Debris\ShinobuVoxelSculptorWindow.cs:679:            densities = new NativeArray<sbyte>(GridCellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\VFX\Debris\ShinobuVoxelSculptorWindow.cs:680:            decompressed = new NativeArray<sbyte>(GridCellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\VFX\Debris\ShinobuVoxelSculptorWindow.cs:681:            accumulator = new NativeArray<int>(GridCellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\VFX\Debris\ShinobuVoxelSculptorWindow.cs:682:            removedMass = new NativeArray<int>(CounterLength, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\VFX\Debris\ShinobuVoxelSculptorWindow.cs:683:            debrisCount = new NativeArray<int>(CounterLength, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\VFX\Debris\ShinobuVoxelSculptorWindow.cs:684:            stats = new NativeArray<int>(StatsLength, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\VFX\Debris\ShinobuVoxelSculptorWindow.cs:685:            writtenCount = new NativeArray<int>(CounterLength, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\VFX\Debris\ShinobuVoxelSculptorWindow.cs:686:            particles = new NativeArray<DebrisParticleDTO>(ShinobuDeltaCrusher.MaximumQualityDebrisCap, Allocator.TempJob, NativeArrayOptions.ClearMemory);

Editor/tool/static suspects:
Total editor/tool/static suspects: 107. Showing first 80. Full list: _scans/09_audio_narrative_presentation_editor_tool_risks.txt.

- Assets\_Project\Scripts\Audio\Editor\SabineReverbDspTunerWindow.cs:215:            return UnityEngine.Object.FindObjectOfType<SpatialAudioManager>();
- Assets\_Project\Scripts\Audio\Synthesis\Editor\VocalStateLayoutValidator.cs:25:            Hecton8.Core.H8Debug.Log("[1308] Vocal bank ABI validated: header=64, record=32, state=32, codec=64, telemetry=64, cue=64.");
- Assets\_Project\Scripts\Audio\Editor\OOP_Voice_Scanner_X_011.cs:28:            new RoutePattern("tmp_text_assignment", ".text =", "warning", "Use SetCharArray through ApplySubtitleBuffer."),
- Assets\_Project\Scripts\Audio\Editor\OOP_Voice_Scanner_X_011.cs:37:            Hecton8.Core.H8Debug.Log("X_011 UX scanner " + (result.Pass ? "PASS" : "FAIL") + " with " + result.Findings.Count + " findings.");
- Assets\_Project\Scripts\Audio\Editor\VocalWarningStormTorture_X_011.cs:15:            Hecton8.Core.H8Debug.Log("X_011 VWS storm torture " + (result.Pass ? "PASS" : "FAIL") + ".");
- Assets\_Project\Scripts\Audio\Synthesis\Editor\DigitalVoiceForgeWindow.cs:121:                _status.text = "voice_baker.py missing.";
- Assets\_Project\Scripts\Audio\Synthesis\Editor\DigitalVoiceForgeWindow.cs:156:            _status.text = "voice_baker.py running.";
- Assets\_Project\Scripts\Audio\Synthesis\Editor\DigitalVoiceForgeWindow.cs:181:                    _status.text = code == 0 ? stdout.Trim() : stderr.Trim();
- Assets\_Project\Scripts\Audio\Synthesis\Editor\DigitalVoiceForgeWindow.cs:188:                _stateLabel.text = string.Concat(
- Assets\_Project\Scripts\Audio\Synthesis\Editor\DigitalVoiceForgeWindow.cs:200:                _stateLabel.text = "Phrase 00000000 | speed 0.00 | volume 0.00 | q 0.00";
- Assets\_Project\Scripts\Audio\Synthesis\Editor\DigitalVoiceForgeWindow.cs:230:            Hecton8.Core.H8Debug.Log("[1308] Digital Voice Forge ABI validation passed.");
- Assets\_Project\Scripts\Audio\Editor\OOP_Voice_Scanner_SHINOBU_352.cs:45:            Hecton8.Core.H8Debug.Log("SHINOBU_352 voice scanner found " + result.Findings.Count + " OOP voice findings; no report files written.");
- Assets\_Project\Scripts\Audio\Editor\DSPThreadSafetySmokeTester.cs:26:                Hecton8.Core.H8Debug.Log(report);
- Assets\_Project\Scripts\Audio\Editor\DSPThreadSafetySmokeTester.cs:28:                Hecton8.Core.H8Debug.LogError(report);
- Assets\_Project\Scripts\Audio\Editor\DSPThreadSafetySmokeTester.cs:157:                AssertNotContains(bufferJobs, ".Complete(", "PlayerCriticalBufferJobs.Clear has no JobHandle.Complete barrier", builder, ref failureCount);
- Assets\_Project\Scripts\Audio\Editor\OOP_AudioSource_Scanner.cs:38:            Hecton8.Core.H8Debug.Log("SHINOBU_351 OOP_AudioSource_Scanner found " + result.ActiveViolationCount + " active violations.");
- Assets\_Project\Scripts\Audio\Editor\OOP_AudioSource_Scanner.cs:194:                        token = "Resources.Load<AudioClip>";
- Assets\_Project\Scripts\Audio\Editor\AudioOmegaAutonomySmokeTester.cs:31:                Hecton8.Core.H8Debug.Log(jsonReport);
- Assets\_Project\Scripts\Audio\Editor\AudioOmegaAutonomySmokeTester.cs:33:                Hecton8.Core.H8Debug.LogError(jsonReport);
- Assets\_Project\Scripts\Audio\Editor\AudioOmegaAutonomySmokeTester.cs:73:            AppendCheck("hot DSP block has no JobHandle completion", !ExtractMethodBody(renderer, "private void MixAndFilterBlock").Contains(".Complete()"), ref passedCount, ref failedCount, checks);
- Assets\_Project\Scripts\Audio\Editor\GranularSynthTunerWindow.cs:207:            return UnityEngine.Object.FindObjectOfType<PlayerCriticalProceduralAudioRenderer>();
- Assets\_Project\Scripts\Audio\Editor\AudioMemorySovereigntyValidator1320.cs:27:            H8Debug.Log("[1320] Procedural audio memory sovereignty validator passed.");
- Assets\_Project\Scripts\Audio\Synthesis\Editor\AbyssalSynthTunerWindow.cs:197:                _statusLabel.text = loaded ? "synth_presets.csv applied." : "synth_presets.csv unavailable.";
- Assets\_Project\Scripts\Audio\Synthesis\Editor\AbyssalSynthTunerWindow.cs:213:            _statusLabel.text =
- Assets\_Project\Scripts\Audio\Synthesis\Editor\AbyssalSynthTunerWindow.cs:311:            return UnityEngine.Object.FindObjectOfType<DynamicMusicGranularSynthesizer>();
- Assets\_Project\Scripts\Audio\Synthesis\Editor\AbyssalSynthTunerWindow.cs:329:                Hecton8.Core.H8Debug.LogError("[1308] SynthVoiceDTO layout violation. Expected explicit 64 bytes with hot fields at offsets 0,4,8,12,16,20 and padding 24-63.");
- Assets\_Project\Scripts\Audio\Synthesis\Editor\AbyssalSynthTunerWindow.cs:331:                Hecton8.Core.H8Debug.Log("[1308] SynthVoiceDTO layout verified: 64 bytes.");
- Assets\_Project\Scripts\Audio\Editor\ShinobuAcousticDspSmokeTester.cs:39:                Hecton8.Core.H8Debug.Log(report);
- Assets\_Project\Scripts\Audio\Editor\ShinobuAcousticDspSmokeTester.cs:41:                Hecton8.Core.H8Debug.LogError(report);
- Assets\_Project\Scripts\Audio\Synthesis\Editor\AudioSynthesisMemorySovereigntyValidator.cs:40:            Hecton8.Core.H8Debug.Log("[1308] Audio synthesis memory sovereignty validator passed.");
- Assets\_Project\Scripts\Audio\Synthesis\Editor\AudioSynthesisMemorySovereigntyValidator.cs:135:                        trimmed.Contains(".Complete(") ||
- Assets\_Project\Scripts\Audio\Synthesis\Editor\AudioSynthesisMemorySovereigntyValidator.cs:136:                        trimmed.Contains("FindObjectOfType") ||
- Assets\_Project\Scripts\Audio\Synthesis\Editor\AudioSynthesisMemorySovereigntyValidator.cs:137:                        trimmed.Contains("GameObject.Find") ||
- Assets\_Project\Scripts\Audio\Synthesis\Editor\AudioSynthesisMemorySovereigntyValidator.cs:138:                        trimmed.Contains("Camera.main") ||
- Assets\_Project\Scripts\Audio\Synthesis\Editor\AudioSynthesisMemorySovereigntyValidator.cs:139:                        trimmed.Contains("GetComponent<") ||
- Assets\_Project\Scripts\Audio\Synthesis\Editor\AudioSynthesisMemorySovereigntyValidator.cs:141:                        trimmed.Contains("StartCoroutine") ||
- Assets\_Project\Scripts\Audio\Synthesis\Editor\AudioSynthesisMemorySovereigntyValidator.cs:143:                        trimmed.Contains("Resources.Load") ||
- Assets\_Project\Scripts\Audio\Synthesis\Editor\AudioSynthesisMemorySovereigntyValidator.cs:145:                        trimmed.Contains("Debug.Log") ||
- Assets\_Project\Scripts\Audio\Synthesis\Editor\AudioSynthesisMemorySovereigntyValidator.cs:146:                        trimmed.Contains("H8Debug.Log") ||
- Assets\_Project\Scripts\Audio\Synthesis\Editor\AudioSynthesisMemorySovereigntyValidator.cs:499:                bank = new NativeArray<byte>(MockBankBytes, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Audio\Synthesis\Editor\AudioSynthesisMemorySovereigntyValidator.cs:500:                records = new NativeArray<VocalBankIndexRecordDTO>(MockRecordCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Audio\Synthesis\Editor\AudioSynthesisMemorySovereigntyValidator.cs:501:                output = new NativeArray<float>(OutputSamples * Channels, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Audio\Synthesis\Editor\AudioSynthesisMemorySovereigntyValidator.cs:502:                state = new NativeArray<VocalStateDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Audio\Synthesis\Editor\AudioSynthesisMemorySovereigntyValidator.cs:503:                codec = new NativeArray<VocalCodecStateDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Audio\Synthesis\Editor\AudioSynthesisMemorySovereigntyValidator.cs:504:                telemetry = new NativeArray<VocalTelemetryEntryDTO>((int)VocalBankConstants.TelemetryRingCapacity, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Audio\Synthesis\Editor\AudioSynthesisMemorySovereigntyValidator.cs:505:                counters = new NativeArray<VocalDecodeCounters64>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Audio\Synthesis\Editor\AudioSynthesisMemorySovereigntyValidator.cs:506:                waveform = new NativeArray<float>(WaveformSamples, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Audio\Editor\Shinobu351HullStressDspSmokeTester.cs:32:                Hecton8.Core.H8Debug.Log(jsonReport);
- Assets\_Project\Scripts\Audio\Editor\Shinobu351HullStressDspSmokeTester.cs:34:                Hecton8.Core.H8Debug.LogError(jsonReport);
- Assets\_Project\Scripts\Audio\Editor\Shinobu351HullStressDspSmokeTester.cs:91:            AppendCheck("kernel contains no Unity AudioSource/AudioClip path", !ContainsAny(kernel, "AudioSource", "AudioClip", "PlayClipAtPoint", "PlayOneShot", "Resources.Load"), ref passedCount, ref failedCount, checks);
- Assets\_Project\Scripts\Audio\Editor\VocalWarningAlarmBitmaskAudit_1629.cs:95:            Hecton8.Core.H8Debug.Log("[1629] VWS alarm bitmask audit PASS.");
- Assets\_Project\Scripts\Audio\Editor\VocalWarningAlarmBitmaskAudit_1629.cs:151:            Require(body.IndexOf("GameObject.Find", StringComparison.Ordinal) < 0, "GameObject.Find in hot method: " + signature);
- Assets\_Project\Scripts\Audio\Editor\AbyssalDspTunerWindow.cs:142:                _statusLabel.text = hasRenderer
- Assets\_Project\Scripts\Audio\Editor\AbyssalDspTunerWindow.cs:287:            return UnityEngine.Object.FindObjectOfType<PlayerCriticalProceduralAudioRenderer>();
- Assets\_Project\Scripts\Audio\Editor\AcousticPortalMemorySovereigntyValidator.cs:40:            H8Debug.Log("[1307] Acoustic portal memory sovereignty validator passed.");
- Assets\_Project\Scripts\Audio\Editor\AcousticPortalMemorySovereigntyValidator.cs:125:                nodes = new NativeArray<AcousticPortalNode>(
- Assets\_Project\Scripts\Audio\Editor\AcousticPortalMemorySovereigntyValidator.cs:129:                edges = new NativeArray<AcousticPortalEdge>(
- Assets\_Project\Scripts\Audio\Editor\AcousticPortalMemorySovereigntyValidator.cs:133:                result = new NativeArray<AcousticPathResult>(
- Assets\_Project\Scripts\Audio\Editor\AcousticPortalMemorySovereigntyValidator.cs:137:                openSet = new NativeArray<int>(
- Assets\_Project\Scripts\Audio\Editor\AcousticPortalMemorySovereigntyValidator.cs:141:                closedSet = new NativeArray<int>(
- Assets\_Project\Scripts\Audio\Editor\AcousticPortalMemorySovereigntyValidator.cs:145:                costs = new NativeArray<float>(
- Assets\_Project\Scripts\Audio\Editor\AcousticPortalMemorySovereigntyValidator.cs:149:                cameFrom = new NativeArray<int>(
- Assets\_Project\Scripts\Audio\Editor\AcousticPortalMemorySovereigntyValidator.cs:153:                states = new NativeArray<byte>(
- Assets\_Project\Scripts\Audio\Editor\AcousticPortalMemorySovereigntyValidator.cs:157:                queries = new NativeArray<AcousticPathQuery>(
- Assets\_Project\Scripts\Audio\Editor\AcousticPortalMemorySovereigntyValidator.cs:175:                loadHandle.Complete();
- Assets\_Project\Scripts\Audio\Editor\AcousticPortalMemorySovereigntyValidator.cs:218:                    pathHandle.Complete();
- Assets\_Project\Scripts\Audio\Editor\AbyssalAcousticsTunerWindow.cs:189:                    _statsLabel.text = "Material CSV missing: " + MaterialCsvAssetPath;
- Assets\_Project\Scripts\Audio\Editor\AbyssalAcousticsTunerWindow.cs:196:                _statsLabel.text = "Material rows loaded: " + rows;
- Assets\_Project\Scripts\Audio\Editor\AbyssalAcousticsTunerWindow.cs:204:            _statsLabel.text =
- Assets\_Project\Scripts\Audio\Editor\AbyssalAcousticsTunerWindow.cs:241:            return UnityEngine.Object.FindObjectOfType<SpatialAudioManager>();
- Assets\_Project\Scripts\Audio\Editor\VocalWarningQueueTunerWindow.cs:139:                    _status.text = "No VocalWarningSystem in loaded scene.";
- Assets\_Project\Scripts\Audio\Editor\VocalWarningQueueTunerWindow.cs:177:                _status.text = _statusBuilder.ToString();
- Assets\_Project\Scripts\Audio\Editor\AdvancedAcousticsSmokeTester.cs:58:                Hecton8.Core.H8Debug.Log(report);
- Assets\_Project\Scripts\Audio\Editor\AdvancedAcousticsSmokeTester.cs:60:                Hecton8.Core.H8Debug.LogError(report);
- Assets\_Project\Scripts\Audio\Editor\AdvancedAcousticsSmokeTester.cs:574:                AssertNotContains(vocalTick, "Debug.Log", "Vocal warning Tick has no debug log allocation path", builder, ref failureCount);
- Assets\_Project\Scripts\Audio\Editor\AdvancedAcousticsSmokeTester.cs:575:                AssertNotContains(vocalSlowTick, "Debug.Log", "Vocal warning SlowTick has no debug log allocation path", builder, ref failureCount);
- Assets\_Project\Scripts\Audio\Editor\AudioImportDictator.cs:81:                Hecton8.Core.H8Debug.LogError(LogUnstable);
- Assets\_Project\Scripts\Audio\Editor\AudioImportDictator.cs:307:            Hecton8.Core.H8Debug.Log("[AudioImportDictator:0xA1D10005] Applied import policy to " +
- Assets\_Project\Scripts\Audio\Editor\AudioImportDictator.cs:590:            Hecton8.Core.H8Debug.LogError(reportText);
- Assets\_Project\Scripts\Audio\Editor\AudioImportDictator.cs:629:            Hecton8.Core.H8Debug.LogError(reportText);

## Exists / Missing / Required Proof

- Exists: bible routes exist and static implementation evidence was found.
- Partial: runtime static risk suspects need manual code review.
- Editor/tool: static suspects exist but may be legal if editor-only or cold-path.
- Required proof: DSP/voice budget proof, soundscape capture, narrative evidence-before-text proof, subtitle/accessibility proof, capture-truth label proof for public material.

## Next Audit Action

Classify each runtime suspect as cold-path/legal or runtime violation. Fix runtime violations before profiler proof.
