using System;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class ScatterRuntimeBackendFacadeEditTests
    {
        [Test]
        public void FacadeSchedulesThroughBackendInterfaceInsteadOfClassicOnlyRoute()
        {
            string facade = ReadProjectFile("Assets/_Project/Scripts/World/ScatterRuntimeBackendFacade.cs");
            string contracts = ReadProjectFile("Assets/_Project/Scripts/World/Contracts/ScatterSimulationContracts.cs");
            string classicAdapter = ReadProjectFile("Assets/_Project/Scripts/World/ScatterClassicBackendAdapters.cs");

            string constructor = ExtractMethodAt(facade, facade.IndexOf("private ScatterRuntimeBackendFacade(\n            ScatterSimulationBackendKind requestedBackendKind", StringComparison.Ordinal));
            string initialize = ExtractMethodAt(facade, facade.IndexOf("public void Initialize()", StringComparison.Ordinal));
            string trySchedule = ExtractMethodAt(facade, facade.IndexOf("public bool TrySchedule(", StringComparison.Ordinal));
            string scheduleKnownBackend = ExtractMethodAt(facade, facade.IndexOf("private static bool TryScheduleKnownBackend", StringComparison.Ordinal));
            string classicInitialize = ExtractMethodAt(classicAdapter, classicAdapter.IndexOf("public void Initialize()", StringComparison.Ordinal));
            string classicTrySchedule = ExtractMethodAt(classicAdapter, classicAdapter.IndexOf("public bool TrySchedule(\n            ScatterSimulationConfig config,\n            NativeArray<float>.ReadOnly heightSamples,", StringComparison.Ordinal));

            StringAssert.Contains("public ScatterRuntimeBackendFacade(\n            IScatterSimulationBackend simulationBackend)", facade);
            StringAssert.Contains("private readonly ScatterSimulationBackendKind _requestedBackendKind;", facade);
            StringAssert.Contains("private readonly uint _backendProviderVersion;", facade);
            StringAssert.Contains(": this(backendKind, CreateSimulationBackend(backendKind))", facade);
            StringAssert.Contains("_requestedBackendKind = requestedBackendKind;", constructor);
            StringAssert.Contains("_backendProviderVersion = ScatterSimulationBackendRegistry.Version;", constructor);
            StringAssert.Contains("_simulationBackend = simulationBackend;", constructor);
            StringAssert.Contains("public ScatterSimulationBackendKind RequestedBackendKind => _requestedBackendKind;", facade);
            StringAssert.Contains("public uint BackendProviderVersion => _backendProviderVersion;", facade);
            StringAssert.Contains("if (_simulationBackend == null)\n                return;", initialize);
            StringAssert.Contains("_initialized = _simulationBackend.IsInitialized;", initialize);
            StringAssert.Contains("bool TrySchedule(", contracts);
            StringAssert.Contains("_initialized = _evaluator.IsInitialized;", classicInitialize);
            StringAssert.Contains("return TryScheduleKnownBackend(_simulationBackend, config, heightSamples, cellStates);", trySchedule);
            StringAssert.Contains("return backend != null && backend.TrySchedule(config, heightSamples, cellStates);", scheduleKnownBackend);
            StringAssert.Contains("return IsInitialized && _evaluator.TryScheduleEvaluation(config, heightSamples, cellStates);", classicTrySchedule);
            StringAssert.DoesNotContain("backend is ScatterClassicSimulationBackend", scheduleKnownBackend);
            StringAssert.DoesNotContain("backend.ForceComplete();", scheduleKnownBackend);
            StringAssert.DoesNotContain("Unknown backend provider cannot be scheduled", scheduleKnownBackend);
        }

        [Test]
        public void BackendRegistryRejectsNullProviderBackendSoFacadeFallbackRemainsReal()
        {
            string registry = ReadProjectFile("Assets/_Project/Scripts/World/Contracts/ScatterSimulationBackendRegistry.cs");
            string facade = ReadProjectFile("Assets/_Project/Scripts/World/ScatterRuntimeBackendFacade.cs");

            string tryCreate = ExtractMethodAt(registry, registry.IndexOf("public static bool TryCreateBackend", StringComparison.Ordinal));
            string createBackend = ExtractMethodAt(facade, facade.IndexOf("private static IScatterSimulationBackend CreateSimulationBackend", StringComparison.Ordinal));

            StringAssert.Contains("private static uint _version;", registry);
            StringAssert.Contains("public static uint Version => _version;", registry);
            StringAssert.Contains("_version++;", registry);
            StringAssert.Contains("if (_provider == null)\n                return false;", tryCreate);
            StringAssert.Contains("if (!_provider.TryCreateBackend(backendKind, out backend) || backend == null)", tryCreate);
            StringAssert.Contains("backend = null;\n                return false;", tryCreate);
            StringAssert.Contains("return true;", tryCreate);
            StringAssert.Contains("if (ScatterSimulationBackendRegistry.TryCreateBackend(backendKind, out IScatterSimulationBackend dotsBackend))\n                        return dotsBackend;", createBackend);
            StringAssert.Contains("return new ScatterClassicSimulationBackend();", createBackend);
        }

        [Test]
        public void SyncFacadeComparesRequestedBackendKindSoProviderFallbackDoesNotThrash()
        {
            string entryPoint = ReadProjectFile("Assets/_Project/Scripts/World/ScatterHybridRuntimeEntryPoint.cs");
            string facade = ReadProjectFile("Assets/_Project/Scripts/World/ScatterRuntimeBackendFacade.cs");

            string syncFacade = ExtractMethodAt(entryPoint, entryPoint.IndexOf("public static ScatterRuntimeBackendFacade SyncFacadeForPlan", StringComparison.Ordinal));
            string shouldReplace = ExtractMethodAt(entryPoint, entryPoint.IndexOf("private static bool ShouldReplaceFacadeForPlan", StringComparison.Ordinal));

            StringAssert.Contains("public ScatterSimulationBackendKind RequestedBackendKind => _requestedBackendKind;", facade);
            StringAssert.Contains("public uint BackendProviderVersion => _backendProviderVersion;", facade);
            StringAssert.Contains("ShouldReplaceFacadeForPlan(currentFacade, in plan)", syncFacade);
            StringAssert.Contains("currentFacade.RequestedBackendKind != plan.ResolvedBackendKind", shouldReplace);
            StringAssert.Contains("plan.ResolvedBackendKind == ScatterSimulationBackendKind.EntitiesDots", shouldReplace);
            StringAssert.Contains("currentFacade.BackendProviderVersion != ScatterSimulationBackendRegistry.Version", shouldReplace);
            StringAssert.DoesNotContain("currentFacade.BackendKind != plan.ResolvedBackendKind", entryPoint);
        }

        [Test]
        public void ClassicScatterEvaluatorSchedulesJobAndCompletesParityResult()
        {
            string evaluator = ReadProjectFile("Assets/_Project/Scripts/World/ScatterEvaluator.cs");

            string initialize = ExtractMethodAt(evaluator, evaluator.IndexOf("public void Initialize()", StringComparison.Ordinal));
            string trySchedule = ExtractMethodAt(evaluator, evaluator.IndexOf("public bool TryScheduleEvaluation", StringComparison.Ordinal));
            string tryComplete = ExtractMethodAt(evaluator, evaluator.IndexOf("public bool TryComplete(out ScatterSimulationResult result)", StringComparison.Ordinal));
            string forceComplete = ExtractMethodAt(evaluator, evaluator.IndexOf("public void ForceComplete()", StringComparison.Ordinal));
            string disposeNativeArray = ExtractMethodAt(evaluator, evaluator.IndexOf("private static unsafe void DisposeNativeArray", StringComparison.Ordinal));
            string job = ExtractMethodAt(evaluator, evaluator.IndexOf("private struct ScatterEvaluationJob", StringComparison.Ordinal));
            string candidateHash = ExtractMethodAt(evaluator, evaluator.IndexOf("private static ulong AccumulateCandidateHash", StringComparison.Ordinal));

            StringAssert.Contains("private NativeArray<ScatterSimulationCandidate> _candidates;", evaluator);
            StringAssert.Contains("private NativeArray<ScatterSimulationParitySnapshot> _paritySnapshots;", evaluator);
            StringAssert.Contains("private NativeArray<float> _heightSamples;", evaluator);
            StringAssert.Contains("private NativeArray<ScatterSimulationCellState> _cellStates;", evaluator);
            StringAssert.Contains("private int _lastCandidateCount;", evaluator);
            StringAssert.Contains("public bool IsInitialized => _initialized && !_disposed;", evaluator);
            StringAssert.Contains("EnsureNativeBuffers();", initialize);
            StringAssert.Contains("_initialized = _candidates.IsCreated && _paritySnapshots.IsCreated;", initialize);
            StringAssert.Contains("H8Memory.Allocate<ScatterSimulationCandidate>", evaluator);
            StringAssert.Contains("H8Memory.Allocate<ScatterSimulationParitySnapshot>", evaluator);
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeMemoryLifetime);", evaluator);

            StringAssert.Contains("public bool TryScheduleEvaluation(", evaluator);
            StringAssert.Contains("NativeArray<ScatterSimulationCellState>.ReadOnly cellStates", trySchedule);
            StringAssert.Contains("int cellCount = cellStates.Length;", trySchedule);
            StringAssert.Contains("heightSamples.Length < cellCount", trySchedule);
            StringAssert.Contains("if (!EnsureInputBuffers(cellCount))", trySchedule);
            StringAssert.Contains("CopyInputSnapshot(heightSamples, cellStates, cellCount);", trySchedule);
            StringAssert.Contains("_activeHandle = new ScatterEvaluationJob", trySchedule);
            StringAssert.Contains("HeightSamples = _heightSamples.GetSubArray(0, cellCount).AsReadOnly()", trySchedule);
            StringAssert.Contains("CellStates = _cellStates.GetSubArray(0, cellCount).AsReadOnly()", trySchedule);
            StringAssert.DoesNotContain("HeightSamples = heightSamples", trySchedule);
            StringAssert.DoesNotContain("CellStates = cellStates", trySchedule);
            StringAssert.Contains("_hasActiveJob = true;", trySchedule);

            StringAssert.Contains("DispatcherJobSwap.TryComplete(ref _activeHandle, forceComplete: false)", tryComplete);
            StringAssert.Contains("_lastCandidateCount = math.clamp(snapshot.CandidateCount", tryComplete);
            StringAssert.Contains("result = new ScatterSimulationResult(_candidates, _lastCandidateCount, snapshot);", tryComplete);
            StringAssert.Contains("if (_paritySnapshots.IsCreated)", forceComplete);
            StringAssert.Contains("DisposeNativeArray(ref _heightSamples, activeHandle, hasActiveJob);", evaluator);
            StringAssert.Contains("DisposeNativeArray(ref _cellStates, activeHandle, hasActiveJob);", evaluator);
            StringAssert.Contains("private bool EnsureInputBuffers(int cellCount)", evaluator);
            StringAssert.Contains("H8Memory.Allocate<float>", evaluator);
            StringAssert.Contains("H8Memory.Allocate<ScatterSimulationCellState>", evaluator);
            StringAssert.Contains("destinationHeightSamples[i] = sourceHeightSamples[i];", evaluator);
            StringAssert.Contains("destinationCellStates[i] = sourceCellStates[i];", evaluator);

            StringAssert.Contains("H8Memory.Release(ref array, dependency, NativeArrayOwner);", disposeNativeArray);
            StringAssert.Contains("H8Memory.Release(ref array, NativeArrayOwner);", disposeNativeArray);
            AssertTextBefore(disposeNativeArray, "H8Memory.Release(ref array, dependency, NativeArrayOwner);", "if (!DispatcherJobFence.TryComplete(ref disposeHandle, forceComplete: true))");
            AssertTextBefore(disposeNativeArray, "if (!DispatcherJobFence.TryComplete(ref disposeHandle, forceComplete: true))", "NativeMemorySentinel.UnregisterPointer(trackedPointer);");
            AssertTextBefore(disposeNativeArray, "H8Memory.Release(ref array, NativeArrayOwner);", "NativeMemorySentinel.UnregisterPointer(trackedPointer);");

            StringAssert.Contains("[ReadOnly, NoAlias] public NativeArray<float>.ReadOnly HeightSamples;", job);
            StringAssert.Contains("[ReadOnly, NoAlias] public NativeArray<ScatterSimulationCellState>.ReadOnly CellStates;", job);
            StringAssert.Contains("AccumulateCellParity(ref snapshot, ref cellHash, in cellState, height);", job);
            StringAssert.Contains("TryEmitLayerCandidates(", job);
            StringAssert.Contains("candidateCount >= Candidates.Length", job);
            StringAssert.Contains("IncrementLayerCount(ref snapshot, layerIndex);", job);
            StringAssert.Contains("ParitySnapshots[0] = snapshot;", job);
            StringAssert.Contains("long classicCellKey = ((long)(uint)(cellState.CellX & 0xFFFF) << 32) | (uint)(cellState.CellZ & 0xFFFF);", candidateHash);
            StringAssert.Contains("hash = Hash(hash, unchecked((ulong)(uint)layerIndex));", candidateHash);
            StringAssert.DoesNotContain("candidate.Position", candidateHash);
            StringAssert.DoesNotContain("candidate.FamilyIndex", candidateHash);
            StringAssert.DoesNotContain("public JobHandle ScheduleEvaluation(", evaluator);
            StringAssert.DoesNotContain("BuildParitySnapshot(", evaluator);
        }

        [Test]
        public void BackendShadowTelemetryDistinguishesCandidateCapacitySaturation()
        {
            string contracts = ReadProjectFile("Assets/_Project/Scripts/World/Contracts/ScatterSimulationContracts.cs");
            string evaluator = ReadProjectFile("Assets/_Project/Scripts/World/ScatterEvaluator.cs");
            string completion = ReadProjectFile("Assets/_Project/Scripts/World/ScatterBackendShadowCompletion.cs");
            string layoutManifest = ReadProjectFile("Assets/_Project/Scripts/Core/BinaryLayoutManifest.cs");

            string job = ExtractMethodAt(evaluator, evaluator.IndexOf("private struct ScatterEvaluationJob", StringComparison.Ordinal));
            string resolveParity = ExtractMethodAt(completion, completion.IndexOf("private static byte ResolveParityStatusCode", StringComparison.Ordinal));

            StringAssert.Contains("public const uint CandidateCapacitySaturatedFlag = 1u;", contracts);
            StringAssert.Contains("[FieldOffset(60)]\n        public uint EvaluationFlags;", contracts);
            StringAssert.Contains("public static bool HasCandidateCapacitySaturated(in ScatterSimulationParitySnapshot snapshot)", contracts);
            StringAssert.Contains("AssertOffset(scatterParity, \"EvaluationFlags\", 60);", layoutManifest);

            StringAssert.Contains("candidateCount >= Candidates.Length", job);
            StringAssert.Contains("if (emitCount < placementsPerCell)\n                    MarkCandidateCapacitySaturated(ref snapshot);", job);
            StringAssert.Contains("private static void MarkCandidateCapacitySaturated(ref ScatterSimulationParitySnapshot snapshot)", job);
            StringAssert.Contains("snapshot.EvaluationFlags |= ScatterSimulationParitySnapshot.CandidateCapacitySaturatedFlag;", job);

            StringAssert.Contains("public const byte ParityStatusBackendCandidateCapacitySaturated = 7;", completion);
            StringAssert.Contains("ScatterSimulationParitySnapshot.HasCandidateCapacitySaturated(in backendParity)", completion);
            StringAssert.Contains("return \"BackendCandidateCapacitySaturated\";", completion);
            StringAssert.Contains("bool backendCandidateCapacitySaturated", resolveParity);
            AssertTextBefore(resolveParity, "if (backendCandidateCapacitySaturated)", "if (candidateDelta != 0)");
        }

        [Test]
        public void InterruptedShadowPassesSurfaceThroughRuntimeStatus()
        {
            string host = ReadProjectFile("Assets/_Project/Scripts/World/ScatterBackendRuntimeHost.cs");
            string status = ReadProjectFile("Assets/_Project/Scripts/World/ScatterBackendRuntimeStatus.cs");
            string director = ReadProjectFile("Assets/_Project/Scripts/WorldProceduralScatterDirector.cs");
            string integration = ReadProjectFile("Assets/_Project/Scripts/WorldProceduralScatterDirectorBackendIntegration.cs");

            string getStatus = ExtractMethodAt(host, host.IndexOf("public ScatterBackendRuntimeStatus GetStatus()", StringComparison.Ordinal));
            string syncFacade = ExtractMethodAt(host, host.IndexOf("public bool SyncFacade()", StringComparison.Ordinal));
            string dispose = ExtractMethodAt(host, host.IndexOf("public void Dispose()", StringComparison.Ordinal));
            string resetHostTelemetry = ExtractMethodAt(host, host.IndexOf("public void ResetTelemetry()", StringComparison.Ordinal));
            string resetDiagnostics = ExtractMethodAt(director, director.IndexOf("private void ResetDiagnostics()", StringComparison.Ordinal));
            string applyStatus = ExtractMethodAt(integration, integration.IndexOf("private void ApplyScatterBackendRuntimeStatus", StringComparison.Ordinal));
            string resetTelemetry = ExtractMethodAt(integration, integration.IndexOf("private void ResetScatterBackendDebugTelemetry", StringComparison.Ordinal));

            StringAssert.Contains("public readonly int InterruptedShadowPassCount;", status);
            StringAssert.Contains("int interruptedShadowPassCount", status);
            StringAssert.Contains("InterruptedShadowPassCount = interruptedShadowPassCount < 0 ? 0 : interruptedShadowPassCount;", status);

            StringAssert.Contains("private int _interruptedShadowPassCount;", host);
            StringAssert.Contains("bool hadActiveFacadeJob = IsFacadeJobActive;", syncFacade);
            StringAssert.Contains("_interruptedShadowPassCount);", getStatus);
            StringAssert.Contains("if (hadActiveFacadeJob)\n                    _interruptedShadowPassCount++;", syncFacade);
            StringAssert.Contains("_interruptedShadowPassCount = 0;", dispose);
            StringAssert.Contains("_interruptedShadowPassCount = 0;", resetHostTelemetry);
            StringAssert.Contains("ClearScheduleFailureReason();", resetHostTelemetry);
            StringAssert.Contains("ClearCompletionFailureReason();", resetHostTelemetry);
            StringAssert.DoesNotContain("Dispose", resetHostTelemetry);
            StringAssert.DoesNotContain("SyncFacade", resetHostTelemetry);

            StringAssert.Contains("[SerializeField] private int _debugScatterBackendShadowInterruptedCount;", director);
            StringAssert.Contains("_scatterBackendHost?.ResetTelemetry();", resetDiagnostics);
            AssertTextBefore(resetDiagnostics, "_scatterBackendHost?.ResetTelemetry();", "ResetScatterBackendDebugTelemetry(backendPlan);");
            StringAssert.Contains("_debugScatterBackendShadowInterruptedCount = status.InterruptedShadowPassCount;", applyStatus);
            StringAssert.Contains("_debugScatterBackendShadowInterruptedCount = 0;", resetTelemetry);
        }

        [Test]
        public void BindingStateExposesOwnerOwnedScatterCellDataSlices()
        {
            string binding = ReadProjectFile("Assets/_Project/Scripts/World/ScatterBackendBindingState.cs");
            string bindingBridge = ReadProjectFile("Assets/_Project/Scripts/World/ScatterBackendBindingBridge.cs");
            string workingMemory = ReadProjectFile("Assets/_Project/Scripts/WorldProceduralScatterWorkingMemory.cs");
            string samplingPipeline = ReadProjectFile("Assets/_Project/Scripts/WorldProceduralScatterDirectorSamplingPipeline.cs");

            string resetLookup = ExtractMethodAt(binding, binding.IndexOf("public void ResetLookup()", StringComparison.Ordinal));
            string registerFamily = ExtractMethodAt(binding, binding.IndexOf("public bool TryRegisterRepresentativeFamilyIndex", StringComparison.Ordinal));
            string tryPopulate = ExtractMethodAt(binding, binding.IndexOf("public bool TryPopulateCellData", StringComparison.Ordinal));
            string clearViews = ExtractMethodAt(binding, binding.IndexOf("public void ClearCellDataViews()", StringComparison.Ordinal));
            string computeFamilyIndex = ExtractMethodAt(bindingBridge, bindingBridge.IndexOf("private static int ComputeFamilyIndex", StringComparison.Ordinal));
            string normalizeFamilyIndex = ExtractMethodAt(bindingBridge, bindingBridge.IndexOf("private static int NormalizeFamilyIndex", StringComparison.Ordinal));
            string ensureCapacity = ExtractMethodAt(workingMemory, workingMemory.IndexOf("public void EnsureCellSamplingCapacity", StringComparison.Ordinal));
            string dispose = ExtractMethodAt(workingMemory, workingMemory.IndexOf("public void Dispose()", StringComparison.Ordinal));

            StringAssert.Contains("public NativeArray<float> ScatterBackendHeightSamples;", workingMemory);
            StringAssert.Contains("EnsureCapacity(ref ScatterBackendHeightSamples, requiredCapacity, nameof(ScatterBackendHeightSamples));", ensureCapacity);
            StringAssert.Contains("DisposeNativeArray(ref ScatterBackendHeightSamples);", dispose);

            StringAssert.Contains("private NativeArray<float>.ReadOnly _heightSamples;", binding);
            StringAssert.Contains("private NativeArray<ScatterSimulationCellState>.ReadOnly _cellStates;", binding);
            StringAssert.Contains("public NativeArray<float>.ReadOnly HeightSamples => _heightSamples;", binding);
            StringAssert.Contains("public NativeArray<ScatterSimulationCellState>.ReadOnly CellStates => _cellStates;", binding);
            StringAssert.Contains("ClearCellDataViews();", resetLookup);
            StringAssert.Contains("family == null || familyIndex <= 0", registerFamily);
            StringAssert.Contains("return NormalizeFamilyIndex(hash);", computeFamilyIndex);
            StringAssert.Contains("int normalized = hash & int.MaxValue;", normalizeFamilyIndex);
            StringAssert.Contains("return normalized == 0 ? 1 : normalized;", normalizeFamilyIndex);
            StringAssert.DoesNotContain("return hash;", computeFamilyIndex);
            StringAssert.Contains("ClearCellDataViews();", tryPopulate);
            StringAssert.Contains("public void ClearCellDataViews()", binding);
            StringAssert.Contains("!memory.ScatterBackendHeightSamples.IsCreated", tryPopulate);
            StringAssert.Contains("memory.CellSamplingOutputs.Length < cellCount", tryPopulate);
            StringAssert.Contains("memory.ScatterBackendHeightSamples.Length < cellCount", tryPopulate);
            StringAssert.Contains("memory.ScatterBackendCellStates.Length < cellCount", tryPopulate);
            StringAssert.Contains("memory.ScatterBackendHeightSamples.Length", tryPopulate);
            StringAssert.Contains("_heightSamples = memory.ScatterBackendHeightSamples.GetSubArray(0, cellCount).AsReadOnly();", tryPopulate);
            StringAssert.Contains("_cellStates = memory.ScatterBackendCellStates.GetSubArray(0, cellCount).AsReadOnly();", tryPopulate);
            StringAssert.DoesNotContain("Mathf.Min(", tryPopulate);
            StringAssert.Contains("_heightSamples = default;", clearViews);
            StringAssert.Contains("_cellStates = default;", clearViews);

            Assert.AreEqual(
                3,
                CountToken(samplingPipeline, "_memory.ScatterBackendHeightSamples[cellIndex] = backendCellState.Height;"));
            AssertTextBefore(
                samplingPipeline,
                "_memory.ScatterBackendCellStates[cellIndex] = backendCellState;",
                "_memory.ScatterBackendHeightSamples[cellIndex] = backendCellState.Height;");
            StringAssert.Contains(
                "if (_candidateBuffer.Count == 0)\n                {\n                    _memory.ScatterBackendCellStates[cellIndex] = backendCellState;\n                    _memory.ScatterBackendHeightSamples[cellIndex] = backendCellState.Height;\n                    continue;\n                }",
                samplingPipeline);
        }

        [Test]
        public void DirectorDisposesScatterBackendBeforeOwnerCellSamplingArrays()
        {
            string director = ReadProjectFile("Assets/_Project/Scripts/WorldProceduralScatterDirector.cs");
            string onDisable = ExtractMethodAt(director, director.IndexOf("private void OnDisable()", StringComparison.Ordinal));
            string onDestroy = ExtractMethodAt(director, director.IndexOf("private void OnDestroy()", StringComparison.Ordinal));
            string editorReload = ExtractMethodAt(director, director.IndexOf("internal void PrepareForEditorReload()", StringComparison.Ordinal));

            AssertTextBefore(onDisable, "CompleteSamplingJobForTeardown();", "DisposeScatterBackendFacade();");
            AssertTextBefore(onDisable, "DisposeScatterBackendFacade();", "DisposeCellSamplingArrays();");
            AssertTextBefore(onDestroy, "CompleteSamplingJobForTeardown();", "DisposeScatterBackendFacade();");
            AssertTextBefore(onDestroy, "DisposeScatterBackendFacade();", "DisposeCellSamplingArrays();");
            AssertTextBefore(editorReload, "CompleteSamplingJobForTeardown();", "DisposeScatterBackendFacade();");
            AssertTextBefore(editorReload, "DisposeScatterBackendFacade();", "DisposeCellSamplingArrays();");
        }

        [Test]
        public void ScheduleFailuresPropagateFromHostIntoDirectorTelemetry()
        {
            string host = ReadProjectFile("Assets/_Project/Scripts/World/ScatterBackendRuntimeHost.cs");
            string director = ReadProjectFile("Assets/_Project/Scripts/WorldProceduralScatterDirectorBackendIntegration.cs");
            string requestFactory = ReadProjectFile("Assets/_Project/Scripts/World/ScatterBackendRequestFactory.cs");

            string getStatus = ExtractMethodAt(host, host.IndexOf("public ScatterBackendRuntimeStatus GetStatus()", StringComparison.Ordinal));
            string syncFacade = ExtractMethodAt(host, host.IndexOf("public bool SyncFacade()", StringComparison.Ordinal));
            string trySchedule = ExtractMethodAt(host, host.IndexOf("public bool TrySchedule(in ScatterBackendScheduleRequest", StringComparison.Ordinal));
            string tryComplete = ExtractMethodAt(host, host.IndexOf("public bool TryCompleteShadowPass", StringComparison.Ordinal));
            string dispose = ExtractMethodAt(host, host.IndexOf("public void Dispose()", StringComparison.Ordinal));
            string markFailure = ExtractMethodAt(host, host.IndexOf("private void MarkScheduleFailure", StringComparison.Ordinal));
            string appendFailure = ExtractMethodAt(host, host.IndexOf("private string AppendScheduleFailureReason", StringComparison.Ordinal));
            string appendCompletionFailure = ExtractMethodAt(host, host.IndexOf("private string AppendCompletionFailureReason", StringComparison.Ordinal));
            string pumpShadowPass = ExtractMethodAt(director, director.IndexOf("private void PumpScatterBackendShadowPass()", StringComparison.Ordinal));
            string scheduleShadowPass = ExtractMethodAt(director, director.IndexOf("private void TryScheduleScatterBackendShadowPass", StringComparison.Ordinal));
            string createRequest = ExtractMethodAt(requestFactory, requestFactory.IndexOf("public ScatterBackendScheduleRequest Create", StringComparison.Ordinal));

            StringAssert.Contains("private string _lastScheduleFailureReason;", host);
            StringAssert.Contains("private string _lastCompletionFailureReason;", host);
            StringAssert.DoesNotContain("SetShadowPendingClassicParity", host);
            StringAssert.Contains("_facade != null && _facade.BackendKind != _plan.ResolvedBackendKind", getStatus);
            StringAssert.Contains("resolutionReason = AppendScheduleFailureReason(resolutionReason);", getStatus);
            StringAssert.Contains("resolutionReason = AppendCompletionFailureReason(resolutionReason);", getStatus);
            StringAssert.Contains("return $\"{resolutionReason}|schedule-failed-{_lastScheduleFailureReason}\";", appendFailure);
            StringAssert.Contains("return $\"{resolutionReason}|completion-failed-{_lastCompletionFailureReason}\";", appendCompletionFailure);
            StringAssert.Contains("_lastScheduleFailureReason = string.IsNullOrWhiteSpace(reason) ? \"unknown\" : reason;", markFailure);
            StringAssert.Contains("ClearCompletionFailureReason();", markFailure);
            StringAssert.Contains("context.ClassicParityReference);", createRequest);

            StringAssert.Contains("MarkScheduleFailure(\"facade-unavailable\");", trySchedule);
            StringAssert.Contains("MarkScheduleFailure(\"backend-not-initialized\");", trySchedule);
            StringAssert.Contains("MarkScheduleFailure(\"binding-state-unavailable\");", trySchedule);
            StringAssert.Contains("MarkScheduleFailure(\"cell-data-unavailable\");", trySchedule);
            StringAssert.Contains("_bindingState.ClearCellDataViews();", trySchedule);
            StringAssert.Contains("MarkScheduleFailure(_facade.IsJobActive ? \"facade-busy\" : \"backend-schedule-rejected\");", trySchedule);
            AssertTextBefore(trySchedule, "_bindingState.ClearCellDataViews();", "MarkScheduleFailure(_facade.IsJobActive ? \"facade-busy\" : \"backend-schedule-rejected\");");
            StringAssert.Contains(
                "return false;\n            }\n\n            _bindingState.ClearCellDataViews();\n            _shadowPendingClassicParity = request.ParityReference;",
                trySchedule);
            StringAssert.Contains("_shadowPendingClassicParity = request.ParityReference;", trySchedule);
            AssertTextBefore(trySchedule, "_shadowPendingClassicParity = request.ParityReference;", "ClearScheduleFailureReason();");
            StringAssert.Contains("ClearScheduleFailureReason();", trySchedule);
            StringAssert.Contains("ClearCompletionFailureReason();", trySchedule);
            AssertTextBefore(trySchedule, "ClearScheduleFailureReason();", "return true;");

            StringAssert.Contains("_bindingState?.ClearCellDataViews();", tryComplete);
            StringAssert.Contains("MarkCompletionFailure(\"backend-complete-failed\");", tryComplete);
            StringAssert.Contains("_facade.Dispose();", tryComplete);
            StringAssert.Contains("_facade = null;", tryComplete);
            StringAssert.Contains("ClearCompletionFailureReason();", tryComplete);
            AssertTextBefore(tryComplete, "_facade.Dispose();", "_facade = null;");
            AssertTextBefore(tryComplete, "_facade = null;", "_shadowPendingClassicParity = default;");
            AssertTextBefore(tryComplete, "_shadowPendingClassicParity = default;", "MarkCompletionFailure(\"backend-complete-failed\");");
            AssertTextBefore(tryComplete, "_bindingState?.ClearCellDataViews();", "MarkCompletionFailure(\"backend-complete-failed\");");
            AssertTextBefore(tryComplete, "_shadowPendingClassicParity = default;", "_bindingState?.ClearCellDataViews();");
            StringAssert.Contains("ClearScheduleFailureReason();", syncFacade);
            StringAssert.Contains("ClearCompletionFailureReason();", syncFacade);
            StringAssert.Contains("_bindingState?.ClearCellDataViews();", syncFacade);
            StringAssert.Contains("ClearScheduleFailureReason();", dispose);
            StringAssert.Contains("ClearCompletionFailureReason();", dispose);
            StringAssert.Contains(
                "if (resetPendingState)\n            {\n                _shadowPendingClassicParity = default;\n                _bindingState?.ClearCellDataViews();\n                ClearScheduleFailureReason();\n                ClearCompletionFailureReason();",
                syncFacade);
            StringAssert.Contains("ApplyScatterBackendShadowCompletion(completion);\n                    return;", pumpShadowPass);
            StringAssert.Contains("ApplyScatterBackendRuntimeStatus(_scatterBackendHost.GetStatus());", pumpShadowPass);

            StringAssert.Contains("if (!_scatterBackendHost.TrySchedule(request, _memory))", scheduleShadowPass);
            StringAssert.Contains("ApplyScatterBackendRuntimeStatus(_scatterBackendHost.GetStatus());\n                    return;", scheduleShadowPass);
            StringAssert.DoesNotContain("_scatterBackendHost.SetShadowPendingClassicParity", scheduleShadowPass);
            StringAssert.Contains("ApplyScatterBackendRuntimeStatus(_scatterBackendHost.GetStatus());\n                _debugScatterBackendShadowPassesScheduled++;\n                _debugScatterBackendShadowPending = true;", scheduleShadowPass);
            AssertTextBefore(scheduleShadowPass, "if (!_scatterBackendHost.TrySchedule(request, _memory))", "ApplyScatterBackendRuntimeStatus(_scatterBackendHost.GetStatus());");
        }

        [Test]
        public void DisabledScatterBackendPlanSynchronizesAndReleasesFacadeBeforeEarlyReturn()
        {
            string host = ReadProjectFile("Assets/_Project/Scripts/World/ScatterBackendRuntimeHost.cs");
            string director = ReadProjectFile("Assets/_Project/Scripts/WorldProceduralScatterDirectorBackendIntegration.cs");

            string syncFacade = ExtractMethodAt(host, host.IndexOf("public bool SyncFacade()", StringComparison.Ordinal));
            string ensureFacade = ExtractMethodAt(director, director.IndexOf("private void EnsureScatterBackendFacadeInitialized()", StringComparison.Ordinal));
            string rebuildLookup = ExtractMethodAt(director, director.IndexOf("private void RebuildScatterBackendLookup()", StringComparison.Ordinal));
            string prepareScheduling = ExtractMethodAt(director, director.IndexOf("private bool TryPrepareScatterBackendShadowScheduling()", StringComparison.Ordinal));

            StringAssert.Contains("if (!_plan.RequiresFacade)\n                    ClearScheduleFailureReason();", syncFacade);
            StringAssert.Contains("ScatterHybridRuntimePlan plan = RefreshScatterBackendPlan();", ensureFacade);
            StringAssert.Contains("if (!plan.RequiresFacade)", ensureFacade);
            StringAssert.Contains("_scatterBackendHost.SyncFacade();\n                ApplyScatterBackendRuntimeStatus(_scatterBackendHost.GetStatus());\n                return;", ensureFacade);
            AssertTextBefore(ensureFacade, "if (!plan.RequiresFacade)", "EnsureScatterBackendSupportContext();");

            StringAssert.Contains("if (!plan.RequiresFacade)", rebuildLookup);
            StringAssert.Contains("_scatterBackendHost.SyncFacade();\n                ApplyScatterBackendRuntimeStatus(_scatterBackendHost.GetStatus());\n                return;", rebuildLookup);
            StringAssert.DoesNotContain("_scatterBackendHost?.ResetBindingLookup();", rebuildLookup);
            AssertTextBefore(rebuildLookup, "_scatterBackendHost.SyncFacade();", "return;");

            StringAssert.Contains("if (!plan.RequiresFacade)", prepareScheduling);
            StringAssert.Contains("_scatterBackendHost.SyncFacade();\n                    ApplyScatterBackendRuntimeStatus(_scatterBackendHost.GetStatus());\n                    return false;", prepareScheduling);
            AssertTextBefore(prepareScheduling, "if (!plan.RequiresFacade)", "EnsureScatterBackendSupportContext();");
        }

        [Test]
        public void ShadowParityReferenceComesFromFinalOwnerDesiredPlacements()
        {
            string director = ReadProjectFile("Assets/_Project/Scripts/WorldProceduralScatterDirector.cs");
            string samplingPipeline = ReadProjectFile("Assets/_Project/Scripts/WorldProceduralScatterDirectorSamplingPipeline.cs");
            string runtimeContexts = ReadProjectFile("Assets/_Project/Scripts/WorldProceduralScatterDirectorRuntimeStateContexts.cs");

            string processing = ExtractMethodAt(samplingPipeline, samplingPipeline.IndexOf("private void ProcessCompletedScatterSampling()", StringComparison.Ordinal));
            string parityBuilder = ExtractMethodAt(director, director.IndexOf("private ScatterBackendParityReference BuildScatterBackendParityReferenceFromDesiredPlacements()", StringComparison.Ordinal));
            string registerPlacement = ExtractMethodAt(runtimeContexts, runtimeContexts.IndexOf("public void Register(ScatterPlacement placement", StringComparison.Ordinal));

            StringAssert.Contains("public void Register(ScatterPlacement placement, WorldPrefabFamilyProfile.ScatterLayer layer)", runtimeContexts);
            StringAssert.Contains("Register(candidate.Placement, layer);", runtimeContexts);
            StringAssert.Contains("long cellKey = ((long)(uint)(placement.CellX & 0xFFFF) << 32) | (uint)(placement.CellZ & 0xFFFF);", registerPlacement);

            StringAssert.Contains("ScatterClassicParityAccumulator accumulator = default;", parityBuilder);
            StringAssert.Contains("Dictionary<long, ScatterPlacement> desiredPlacements = _desiredPlacements;", parityBuilder);
            StringAssert.Contains("List<long> sortedPlacementKeys = _removalBuffer;", parityBuilder);
            StringAssert.Contains("sortedPlacementKeys.Sort();", parityBuilder);
            StringAssert.Contains("for (int i = 0; i < sortedPlacementKeys.Count; i++)", parityBuilder);
            StringAssert.Contains("desiredPlacements.TryGetValue(sortedPlacementKeys[i], out ScatterPlacement placement)", parityBuilder);
            StringAssert.Contains("return accumulator.ToReference();", parityBuilder);
            StringAssert.Contains("accumulator.Register(placement, placement.Family.scatterLayer);", parityBuilder);

            StringAssert.Contains("ScatterBackendParityReference finalOwnerParityReference = BuildScatterBackendParityReferenceFromDesiredPlacements();", processing);
            StringAssert.Contains("finalOwnerParityReference));", processing);
            StringAssert.DoesNotContain("classicParityAccumulator", samplingPipeline);
            AssertTextBefore(processing, "ScatterReconcileMetrics reconcileMetrics = ReconcileInstances(enableScatterRebuildProfiling);", "ScatterBackendParityReference finalOwnerParityReference = BuildScatterBackendParityReferenceFromDesiredPlacements();");
            AssertTextBefore(processing, "ApplyCompletedScatterSamplingDebugState(", "ScatterBackendParityReference finalOwnerParityReference = BuildScatterBackendParityReferenceFromDesiredPlacements();");
            AssertTextBefore(processing, "ScatterBackendParityReference finalOwnerParityReference = BuildScatterBackendParityReferenceFromDesiredPlacements();", "TryScheduleScatterBackendShadowPass(new ScatterBackendShadowScheduleContext(");
        }

        private static string ReadProjectFile(string relativePath)
        {
            return File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), relativePath))
                .Replace("\r\n", "\n");
        }

        private static string ExtractMethodAt(string source, int methodStart)
        {
            Assert.GreaterOrEqual(methodStart, 0, "Missing method start.");

            int open = source.IndexOf('{', methodStart);
            Assert.GreaterOrEqual(open, 0, "Missing method open brace.");

            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                char c = source[i];
                if (c == '{')
                    depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(open, i - open + 1);
                }
            }

            Assert.Fail("Missing method close brace.");
            return string.Empty;
        }

        private static int CountToken(string source, string token)
        {
            int count = 0;
            int index = 0;
            while (true)
            {
                index = source.IndexOf(token, index, StringComparison.Ordinal);
                if (index < 0)
                    return count;

                count++;
                index += token.Length;
            }
        }

        private static void AssertTextBefore(string source, string before, string after)
        {
            int beforeIndex = source.IndexOf(before, StringComparison.Ordinal);
            int afterIndex = source.IndexOf(after, StringComparison.Ordinal);
            Assert.GreaterOrEqual(beforeIndex, 0, "Missing expected text: " + before);
            Assert.GreaterOrEqual(afterIndex, 0, "Missing expected text: " + after);
            Assert.Less(beforeIndex, afterIndex, before + " must appear before " + after);
        }
    }
}
