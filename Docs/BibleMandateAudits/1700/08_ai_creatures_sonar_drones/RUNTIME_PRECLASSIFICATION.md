# Runtime Preclassification - AI, Creatures, Sonar, Drones, Navigation

Status: HEURISTIC FIRST PASS - MANUAL REVIEW STILL REQUIRED
Date: 2026-06-02

This file groups static runtime suspects by a conservative heuristic. It can reduce review time, but it cannot prove a line is legal or illegal without reading the containing method and owner phase.

Total runtime suspects: 70.

## Summary

- LEGAL_EDITOR_OR_DEV_GUARDED: 42
- REVIEW_NATIVE_LIFETIME_OR_ALLOCATOR_PROOF_REQUIRED: 12
- REVIEW_CACHE_OR_INJECTION_REQUIRED: 9
- LIKELY_LEGAL_COLD_OR_OWNER_LIFETIME_PATH: 5
- REVIEW_RUNTIME_MESH_MATERIAL_PATH: 1
- REVIEW_LOG_GUARD_REQUIRED: 1

## LEGAL_EDITOR_OR_DEV_GUARDED (42)

- Runtime debug logging | Assets\_Project\Scripts\Fauna\FaunaPOI.cs:53:                Hecton8.Core.H8Debug.LogError("FaunaPOI editor validation watchdog tripped.", this);
- Runtime debug logging | Assets\_Project\Scripts\Fauna\FaunaSpeciesProfile.cs:127:            Hecton8.Core.H8Debug.LogWarning(
- Runtime debug logging | Assets\_Project\Scripts\Fauna\FaunaBrain.cs:5063:                Hecton8.Core.H8Debug.LogError("FaunaBrain slow-tick watchdog tripped. Cadence backlog was clamped.", this);
- Runtime debug logging | Assets\_Project\Scripts\Fauna\FaunaBrain.cs:6121:                Hecton8.Core.H8Debug.Log("[FAUNA] Feed event. Entering SATED state.", this);
- Runtime mesh/material mutation | Assets\_Project\Scripts\Fauna\FaunaBrain.cs:7760:                Hecton8.Core.H8Debug.LogError("FaunaBrain requires primitive collider hygiene. MeshCollider detected on fauna hierarchy.", meshCollider);
- Runtime debug logging | Assets\_Project\Scripts\Fauna\FaunaBrain.cs:7772:                Hecton8.Core.H8Debug.LogError("FaunaBrain requires a CapsuleCollider or SphereCollider on the fauna hierarchy.", this);
- Runtime debug logging | Assets\_Project\Scripts\Ecosystem\MacroEcosystemMathematicianRuntime_SHINOBU300_Audit.cs:121:                Hecton8.Core.H8Debug.Log("[SHINOBU_300] Macro ecosystem self audit passed.");
- Runtime debug logging | Assets\_Project\Scripts\Ecosystem\MacroEcosystemMathematicianRuntime_SHINOBU300_Audit.cs:123:                Hecton8.Core.H8Debug.LogError("[SHINOBU_300] Macro ecosystem self audit failed.");
- Runtime debug logging | Assets\_Project\Scripts\Tools\SceneTransitionVerifier.cs:162:                Hecton8.Core.H8Debug.LogException(exception);
- Runtime debug logging | Assets\_Project\Scripts\Tools\SceneTransitionVerifier.cs:198:                Hecton8.Core.H8Debug.Log($"[SceneTransitionVerifier] {message}");
- Runtime debug logging | Assets\_Project\Scripts\Tools\PauseSystemVerifier.cs:217:                Hecton8.Core.H8Debug.LogException(exception);
- Runtime debug logging | Assets\_Project\Scripts\Tools\PauseSystemVerifier.cs:400:                Hecton8.Core.H8Debug.Log($"[PauseSystemVerifier] {message}");
- Runtime debug logging | Assets\_Project\Scripts\Tools\PauseSystemVerifier.cs:410:                Hecton8.Core.H8Debug.Log(isPaused ? PauseChangedPausedLog : PauseChangedUnpausedLog);
- Runtime debug logging | Assets\_Project\Scripts\Tools\PerformanceMonitor.cs:360:            Hecton8.Core.H8Debug.Log($"[PerformanceMonitor] Started performance capture | targetFrames={targetFrameCount}");
- Runtime debug logging | Assets\_Project\Scripts\Tools\PerformanceMonitor.cs:367:                Hecton8.Core.H8Debug.LogWarning("[PerformanceMonitor] Capture completed with no samples recorded");
- Runtime debug logging | Assets\_Project\Scripts\Tools\PerformanceMonitor.cs:371:            Hecton8.Core.H8Debug.Log($"[PerformanceMonitor] Capture complete | samples={sampleCount}\n{snapshot.ToDetailedString()}");
- Runtime debug logging | Assets\_Project\Scripts\Tools\PerformanceMonitor.cs:376:            Hecton8.Core.H8Debug.Log("[PerformanceMonitor] Current: " + currentFrameTimeMs.ToString("F2", CultureInfo.InvariantCulture) + "ms | samples=" + sampleCount);
- Runtime debug logging | Assets\_Project\Scripts\Tools\StateRecoveryVerifier.cs:158:                Hecton8.Core.H8Debug.LogException(exception, this);
- Runtime debug logging | Assets\_Project\Scripts\Tools\StateRecoveryVerifier.cs:520:                Hecton8.Core.H8Debug.Log($"[StateRecoveryVerifier] {message}");
- Runtime debug logging | Assets\_Project\Scripts\Tools\PerformanceBudgetController.cs:651:            Hecton8.Core.H8Debug.LogWarning($"[PerformanceBudgetController] System '{systemName}' already registered");
- Runtime debug logging | Assets\_Project\Scripts\Tools\PerformanceBudgetController.cs:656:            Hecton8.Core.H8Debug.LogWarning($"[PerformanceBudgetController] Ignoring invalid registration '{systemName}'");
- Runtime debug logging | Assets\_Project\Scripts\Tools\PerformanceBudgetController.cs:661:            Hecton8.Core.H8Debug.LogWarning($"[PerformanceBudgetController] Ignoring registration '{systemName}' because budget capacity {MaxTrackedBudgetSystems} is full");
- Runtime debug logging | Assets\_Project\Scripts\Tools\PerformanceBudgetController.cs:666:            Hecton8.Core.H8Debug.Log("[PerformanceBudgetController] Registered system '" + systemName + "' with " + budgetMs.ToString("F2", CultureInfo.InvariantCulture) + "ms budget");
- Runtime debug logging | Assets\_Project\Scripts\Tools\PerformanceBudgetController.cs:671:            Hecton8.Core.H8Debug.Log($"[PerformanceBudgetController] Unregistered system '{systemName}'");
- Runtime debug logging | Assets\_Project\Scripts\Tools\PerformanceBudgetController.cs:676:            Hecton8.Core.H8Debug.LogWarning("[PerformanceBudgetController] System '" + systemName + "' over budget: " +
- Runtime debug logging | Assets\_Project\Scripts\Tools\PerformanceBudgetController.cs:683:            Hecton8.Core.H8Debug.Log("[PerformanceBudgetController] Reducing system '" + systemName +
- Runtime debug logging | Assets\_Project\Scripts\Tools\PerformanceBudgetController.cs:693:            Hecton8.Core.H8Debug.Log($"[PerformanceBudgetController] Restoring system '{systemName}' performance");
- Runtime debug logging | Assets\_Project\Scripts\Tools\PerformanceBudgetController.cs:698:            Hecton8.Core.H8Debug.Log(DescribeStatus());
- Runtime debug logging | Assets\_Project\Scripts\Tools\ToolKinematics\ToolKinematicsRuntime.cs:1225:                Hecton8.Core.H8Debug.LogError("[ToolKinematicsRuntime] ARM64 DTO layout mismatch. Runtime disabled.");
- Runtime debug logging | Assets\_Project\Scripts\Audio\AdaptiveStem\AdaptiveStemAudioMixer.cs:1315:                Hecton8.Core.H8Debug.LogWarning("[SHINOBU_46] Failed to dump adaptive stem telemetry.");
- Runtime debug logging | Assets\_Project\Scripts\Audio\AdaptiveStem\AdaptiveStemAudioMixer.cs:1319:                Hecton8.Core.H8Debug.LogWarning("[SHINOBU_46] Failed to dump adaptive stem telemetry.");
- Runtime debug logging | Assets\_Project\Scripts\Audio\AdaptiveStem\AdaptiveStemAudioMixer.cs:1372:                Hecton8.Core.H8Debug.LogWarning("[SHINOBU_46] audio_stem_rules.csv parse failed.");
- Runtime debug logging | Assets\_Project\Scripts\Audio\AdaptiveStem\AdaptiveStemAudioMixer.cs:1376:                Hecton8.Core.H8Debug.LogWarning("[SHINOBU_46] audio_stem_rules.csv parse failed.");
- Runtime debug logging | Assets\_Project\Scripts\Audio\HectonMusicDirector.cs:907:                    Hecton8.Core.H8Debug.LogError("[HectonMusicDirector] Missing authored HectonMusicDirectorConfig for active scene.");
- Runtime debug logging | Assets\_Project\Scripts\Audio\HectonMusicDirector.cs:917:                Hecton8.Core.H8Debug.LogError("[HectonMusicDirector] Missing authored RuntimeDirectorPrefab on active HectonMusicDirectorConfig.");
- Runtime debug logging | Assets\_Project\Scripts\Audio\ProceduralAudioEvents.cs:1267:            Hecton8.Core.H8Debug.LogException(exception);
- Runtime debug logging | Assets\_Project\Scripts\Audio\PlayerCriticalProceduralAudioRenderer.cs:3493:            Hecton8.Core.H8Debug.LogWarning("[PlayerCriticalProceduralAudioRenderer] Audio producer thread failed to stop within watchdog budget. Native audio buffers remain owned until the worker exits.");
- Runtime debug logging | Assets\_Project\Scripts\Audio\PlayerCriticalProceduralAudioRenderer.cs:4514:                    Hecton8.Core.H8Debug.LogWarning("[PlayerCriticalProceduralAudioRenderer] Missing authored AudioReverbFilter. RequireComponent should install it before runtime; reverb fallback is disabled.", this);
- Runtime debug logging | Assets\_Project\Scripts\Audio\PlayerCriticalProceduralAudioRenderer.cs:4561:                    Hecton8.Core.H8Debug.LogWarning("[PlayerCriticalProceduralAudioRenderer] Reverb control mixer is missing one or more exposed parameters. Falling back to AudioReverbFilter.", this);
- Runtime debug logging | Assets\_Project\Scripts\Audio\PlayerCriticalProceduralAudioRenderer.cs:4582:                Hecton8.Core.H8Debug.LogWarning("[PlayerCriticalProceduralAudioRenderer] Reverb wet-mix parameter missing on AudioMixer. Decay/room parameters stay mixer-driven, wet mix falls back to the default mixer state.", this);
- Additional lines omitted here: 2. Use `../_scans/08_ai_creatures_sonar_drones_runtime_risks.txt` for the full list.

## REVIEW_NATIVE_LIFETIME_OR_ALLOCATOR_PROOF_REQUIRED (12)

- Native allocation or persistent lifetime | Assets\_Project\Scripts\AI\Ecosystem\ShinobuEcosystemBalancer.cs:1444:            array = new NativeArray<T>(length, Allocator.Persistent, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\AI\Ecosystem\ShinobuFloraFaunaSymbiosisSolver.cs:740:            array = new NativeArray<T>(length, Allocator.Persistent, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Tools\WfcLaserCutRuntime.cs:623:                payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Tools\ToolKinematics\ToolKinematicsRuntime.cs:957:            NativeArray<byte> bytes = new NativeArray<byte>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Audio\NativeAudioFrameRingBuffer.cs:506:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Audio\NativeAudioFrameRingBuffer.cs:515:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Audio\NativeAudioFrameRingBuffer.cs:527:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Audio\NativeAudioFrameRingBuffer.cs:539:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Audio\NativeAudioFrameRingBuffer.cs:565:                H8Memory.FreeRaw(_telemetryDumpBytesPtr, Allocator.Persistent, VaultOwner);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Audio\NativeAudioFrameRingBuffer.cs:571:                H8Memory.FreeRaw(_telemetryPtr, Allocator.Persistent, VaultOwner);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Audio\NativeAudioFrameRingBuffer.cs:577:                H8Memory.FreeRaw(_sharedStatePtr, Allocator.Persistent, VaultOwner);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Audio\NativeAudioFrameRingBuffer.cs:583:                H8Memory.FreeRaw(_framesPtr, Allocator.Persistent, VaultOwner);

## REVIEW_CACHE_OR_INJECTION_REQUIRED (9)

- Unity scene lookup | Assets\_Project\Scripts\Fauna\CreatureDamageManager.cs:229:            GetComponentsInChildren(true, _rendererScratch);
- Unity scene lookup | Assets\_Project\Scripts\Fauna\FaunaBrain.cs:4425:            GetComponentsInChildren(true, _biolumPresentationLightScratch);
- Unity scene lookup | Assets\_Project\Scripts\Fauna\FaunaBrain.cs:7434:            GetComponentsInChildren(true, _logicalLodColliderScratch);
- Unity scene lookup | Assets\_Project\Scripts\Ecosystem\EcosystemRuntimeInstaller.cs:24:            if (!runtimeRoot.TryGetComponent<FaunaGeneticsManager>(out _))
- Unity scene lookup | Assets\_Project\Scripts\Ecosystem\EcosystemRuntimeInstaller.cs:27:            if (!runtimeRoot.TryGetComponent<EcosystemHealthDirector>(out _))
- Unity scene lookup | Assets\_Project\Scripts\Ecosystem\EcosystemRuntimeInstaller.cs:30:            if (!runtimeRoot.TryGetComponent<MigrationDirector>(out _))
- Unity scene lookup | Assets\_Project\Scripts\Ecosystem\EcosystemRuntimeInstaller.cs:33:            if (!runtimeRoot.TryGetComponent<EcosystemPopulationBalancer>(out _))
- Unity scene lookup | Assets\_Project\Scripts\Audio\Synthesis\VocalBankPlaybackRuntime.cs:306:            bool hasListener = TryGetComponent<AudioListener>(out _);
- Unity scene lookup | Assets\_Project\Scripts\Audio\Synthesis\DynamicMusic\DynamicMusicGranularSynthesizer.cs:681:            if (!TryGetComponent<AudioListener>(out _))

## LIKELY_LEGAL_COLD_OR_OWNER_LIFETIME_PATH (5)

- Native allocation or persistent lifetime | Assets\_Project\Scripts\AI\Ecosystem\ShinobuEcosystemBalancer.cs:5047:            s_snapshotBuffer = new NativeArray<byte>(DumpSnapshotBytes, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\AI\Ecosystem\ShinobuSpatialGridSolver.cs:1724:            s_snapshotBuffer = new NativeArray<byte>(DumpSnapshotBytes, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Tools\LaserCutterDodRuntime.cs:1077:                NativeArray<byte> payload = new NativeArray<byte>(totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Audio\Synthesis\VocalBankPlaybackRuntime.cs:1355:                _editorCsvScratch = (byte*)UnsafeUtility.Malloc(EditorCsvScratchBytes, 16, Allocator.Persistent);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Audio\Synthesis\VocalBankPlaybackRuntime.cs:1367:                UnsafeUtility.Free(_editorCsvScratch, Allocator.Persistent);

## REVIEW_RUNTIME_MESH_MATERIAL_PATH (1)

- Runtime mesh/material mutation | Assets\_Project\Scripts\Fauna\FaunaBrain.cs:7756:            MeshCollider meshCollider = ComponentReferenceUtility.ResolveOwnedComponent<MeshCollider>(transform);

## REVIEW_LOG_GUARD_REQUIRED (1)

- Runtime debug logging | Assets\_Project\Scripts\Fauna\PredatorCognitionDomain_Steering.cs:1765:            Debug.Log("[OOP_Movement_Scanner] scanned Update scopes=" + updateScopes.ToString() +
