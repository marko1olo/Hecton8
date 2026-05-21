#if UNITY_EDITOR
using System;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.World.ShinobuBiomimetic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.World.ShinobuBiomimetic.Editor
{
    public sealed class ShinobuPoiTopologyTunerWindow : EditorWindow
    {
        private const int DefaultRuleCapacity = 256;
        private const int DefaultBoundsCapacity = 256;
        private const int DefaultPreviewLimit = 256;
        private const SystemID PoiOwnerSystem = SystemID.WorldStreaming;
        private const string CsvPath = "poi_spawn_rules.csv";

        private float _globalDensity = 1f;
        private float _debrisScatterRadius = 32f;
        private float _maxSlopeTolerance = ShinobuPoiConstants.DefaultMaxSlopeDegrees;
        private float _currentOverride;
        private int _previewLimit = DefaultPreviewLimit;
        private DateTime _lastCsvWriteUtc;
        private double _nextCsvPollTime;
        private int _lastImportedRules;
        private bool _drawGizmos = true;
        private JobHandle _queuedBakeFenceHandle;
        private bool _hasQueuedBakeFence;
        private int _lastGeneratedPoiCount;
        private int _lastAnchorCount;
        private Label _rulesLabel;
        private Label _bakeFenceLabel;
        private Label _placementLabel;

        [MenuItem("HECTON-8/World/POI Topology Tuner")]
        public static void Open()
        {
            GetWindow<ShinobuPoiTopologyTunerWindow>("POI Topology Tuner");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui -= OnSceneGui;
            SceneView.duringSceneGui += OnSceneGui;
            EditorApplication.update -= PollCsv;
            EditorApplication.update += PollCsv;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGui;
            EditorApplication.update -= PollCsv;
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();

            Slider globalDensity = new Slider("Global Density", 0f, 1f) { value = _globalDensity };
            globalDensity.RegisterValueChangedCallback(OnGlobalDensityChanged);
            root.Add(globalDensity);

            Slider debrisScatterRadius = new Slider("Debris Scatter Radius", 4f, 160f) { value = _debrisScatterRadius };
            debrisScatterRadius.RegisterValueChangedCallback(OnDebrisScatterRadiusChanged);
            root.Add(debrisScatterRadius);

            Slider maxSlopeTolerance = new Slider("Max Slope Tolerance", 1f, 45f) { value = _maxSlopeTolerance };
            maxSlopeTolerance.RegisterValueChangedCallback(OnMaxSlopeToleranceChanged);
            root.Add(maxSlopeTolerance);

            Slider currentOverride = new Slider("Current Override", -1f, 1f) { value = _currentOverride };
            currentOverride.RegisterValueChangedCallback(OnCurrentOverrideChanged);
            root.Add(currentOverride);

            SliderInt previewLimit = new SliderInt("Preview Limit", 1, 2048) { value = _previewLimit };
            previewLimit.RegisterValueChangedCallback(OnPreviewLimitChanged);
            root.Add(previewLimit);

            Toggle drawGizmos = new Toggle("Draw Gizmos") { value = _drawGizmos };
            drawGizmos.RegisterValueChangedCallback(OnDrawGizmosChanged);
            root.Add(drawGizmos);

            root.Add(new Button(SyncConfigToVault) { text = "Sync Vault" });
            root.Add(new Button(ImportCsvRules) { text = "Import CSV" });
            root.Add(new Button(QueueLocalPlacementBake) { text = "Run Placement Bake" });
            root.Add(new Button(DumpBlackBox) { text = "Dump Black Box" });

            _rulesLabel = new Label();
            _bakeFenceLabel = new Label();
            _placementLabel = new Label();
            root.Add(_rulesLabel);
            root.Add(_bakeFenceLabel);
            root.Add(_placementLabel);
            root.Add(new Label("PoiTransformDTO: 64b AUP@0 Rotation@24 Scale@40 Prefab@52 Biome@56 Quest@60"));
            root.Add(new Label("StructuralBoundsDTO: 32b Extents@0 Center@12 Clearance@24 Pad@28"));
            root.Add(new Label("PoiOfflineBakeConfigDTO: 80b Scalars@0..60 RequiredMask@64 Pad@72"));
            RefreshStatusLabels();
        }

        private void OnGlobalDensityChanged(ChangeEvent<float> evt)
        {
            _globalDensity = evt.newValue;
        }

        private void OnDebrisScatterRadiusChanged(ChangeEvent<float> evt)
        {
            _debrisScatterRadius = evt.newValue;
        }

        private void OnMaxSlopeToleranceChanged(ChangeEvent<float> evt)
        {
            _maxSlopeTolerance = evt.newValue;
        }

        private void OnCurrentOverrideChanged(ChangeEvent<float> evt)
        {
            _currentOverride = evt.newValue;
        }

        private void OnPreviewLimitChanged(ChangeEvent<int> evt)
        {
            _previewLimit = evt.newValue;
        }

        private void OnDrawGizmosChanged(ChangeEvent<bool> evt)
        {
            _drawGizmos = evt.newValue;
        }

        private void RefreshStatusLabels()
        {
            if (_rulesLabel != null)
                _rulesLabel.text = "Rules: " + _lastImportedRules.ToString();
            if (_bakeFenceLabel != null)
                _bakeFenceLabel.text = _hasQueuedBakeFence ? "Bake Fence: Queued" : "Bake Fence: Idle";
            if (_placementLabel != null)
                _placementLabel.text = "POIs: " + _lastGeneratedPoiCount.ToString() + " Anchors: " + _lastAnchorCount.ToString();
        }

        private void PollCsv()
        {
            TryRetireQueuedBake();
            if (HasPendingBake())
                return;

            if (EditorApplication.timeSinceStartup < _nextCsvPollTime)
                return;

            _nextCsvPollTime = EditorApplication.timeSinceStartup + 1.0;
            if (!File.Exists(CsvPath))
                return;

            DateTime writeUtc = File.GetLastWriteTimeUtc(CsvPath);
            if (writeUtc == _lastCsvWriteUtc)
                return;

            _lastCsvWriteUtc = writeUtc;
            ImportCsvRules();
            Repaint();
        }

        private void SyncConfigToVault()
        {
            if (HasPendingBake())
                return;

            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
                return;

            if (!TryEnsurePoiVaultBuffer(
                    vault,
                    ShinobuPoiVaultBridge.PoiBakeConfigBufferId,
                    1,
                    NativeArrayOptions.UninitializedMemory,
                    out NativeArray<PoiOfflineBakeConfigDTO> config))
            {
                return;
            }

            config[0] = new PoiOfflineBakeConfigDTO
            {
                Seed = 2166136261u,
                CandidateCount = 5000,
                MaxPoiTransforms = 50000,
                GlobalQualityWeight = math.saturate(_globalDensity),
                DebrisScatterRadiusMeters = _debrisScatterRadius,
                AnchorSampleStrideMeters = 0f,
                MinimumAnchorScore = ShinobuPoiConstants.DefaultVisualAnchorScore,
                MaxSlopeDegreesOverride = _maxSlopeTolerance,
                BiomeAgeHash = 0xB10A9E30u,
                Flags = ShinobuPoiConstants.FlagOfflineBake,
                MaxDebrisPerMajor = 50,
                TelemetryRingLength = ShinobuPoiVaultBridge.BlackBoxFrameCount,
                SectorHashMapCapacity = 65536,
                SectorGridStrideX = 128,
                CurrentOverride = _currentOverride,
                _pad0 = 0u,
                RequiredBufferMask = 0u
            };
        }

        private static bool TryEnsurePoiVaultBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            int length = math.max(1, requiredLength);
            if (vault == null || vault.IsAllocationLocked || vault.IsCompactionFenceActive)
                return false;

            VaultGenerationHandle<T> handle = vault.GetGenerationHandle<T>(
                bufferId,
                length,
                PoiOwnerSystem,
                options);
            return TryResolvePoiVaultBuffer(vault, in handle, bufferId, length, out buffer);
        }

        private static bool TryReadPoiVaultBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null ||
                requiredLength <= 0 ||
                vault.IsCompactionFenceActive ||
                !vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> handle) ||
                !IsPoiVaultHandle(in handle, bufferId) ||
                !vault.TryReadHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool TryResolveExistingPoiVaultBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null ||
                requiredLength <= 0 ||
                vault.IsCompactionFenceActive ||
                !vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> handle))
            {
                return false;
            }

            return TryResolvePoiVaultBuffer(vault, in handle, bufferId, requiredLength, out buffer);
        }

        private static bool TryResolvePoiVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null ||
                requiredLength <= 0 ||
                !IsPoiVaultHandle(in handle, bufferId) ||
                !vault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool IsPoiVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID bufferId) where T : struct
        {
            return handle.BufferID == (uint)bufferId &&
                   handle.SystemID == (uint)PoiOwnerSystem &&
                   handle.Generation != 0u;
        }

        private void ImportCsvRules()
        {
            if (HasPendingBake())
                return;

            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
                return;

            if (!TryEnsurePoiVaultBuffer(
                    vault,
                    ShinobuPoiVaultBridge.PoiRulesBufferId,
                    DefaultRuleCapacity,
                    NativeArrayOptions.UninitializedMemory,
                    out NativeArray<PoiPlacementRuleDTO> rules) ||
                !TryEnsurePoiVaultBuffer(
                    vault,
                    ShinobuPoiVaultBridge.PoiBoundsBufferId,
                    DefaultBoundsCapacity,
                    NativeArrayOptions.UninitializedMemory,
                    out NativeArray<StructuralBoundsDTO> bounds))
            {
                return;
            }

            _lastImportedRules = TryReadCsvBytes(vault, out NativeArray<byte> bytes, out int byteCount)
                ? ShinobuPoiCsvRulesIngestor.Parse(bytes, byteCount, rules, bounds)
                : ShinobuPoiEmergencyRules.GenerateEmergencyMockRules(rules, bounds);
            RefreshStatusLabels();
        }

        private static unsafe bool TryReadCsvBytes(IDataVault vault, out NativeArray<byte> bytes, out int byteCount)
        {
            bytes = default;
            byteCount = 0;
            if (vault == null || !File.Exists(CsvPath))
                return false;

            try
            {
                using (FileStream stream = new FileStream(CsvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    long length = stream.Length;
                    if (length <= 0L || length > int.MaxValue)
                        return false;

                    byteCount = (int)length;
                    if (!TryEnsurePoiVaultBuffer(
                            vault,
                            ShinobuPoiVaultBridge.PoiCsvScratchBufferId,
                            byteCount,
                            NativeArrayOptions.UninitializedMemory,
                            out bytes))
                    {
                        return false;
                    }

                    byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(bytes);
                    Span<byte> target = new Span<byte>(ptr, byteCount);
                    int offset = 0;
                    while (offset < byteCount)
                    {
                        int read = stream.Read(target.Slice(offset));
                        if (read <= 0)
                            break;
                        offset += read;
                    }

                    byteCount = offset;
                }
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }

            return bytes.IsCreated && byteCount > 0;
        }

        private void DumpBlackBox()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (!TryReadPoiVaultBuffer(
                    vault,
                    ShinobuPoiVaultBridge.PoiTelemetryRingBufferId,
                    ShinobuPoiVaultBridge.BlackBoxFrameCount,
                    out NativeArray<PoiPlacementTelemetryEntry> telemetry))
            {
                return;
            }

            ShinobuPoiTelemetryDump.TryDumpTelemetryRing(telemetry);
            ShinobuPoiTelemetryDump.TryDumpPromptAlias(telemetry);
        }

        private void QueueLocalPlacementBake()
        {
            if (HasPendingBake())
                return;

            SyncConfigToVault();
            ImportCsvRules();

            IDataVault vault = GlobalRegistry.DataVault;
            if (!TryReadPoiVaultBuffer(
                    vault,
                    ShinobuPoiVaultBridge.PoiBakeConfigBufferId,
                    1,
                    out NativeArray<PoiOfflineBakeConfigDTO> config) ||
                !TryReadPoiVaultBuffer(
                    vault,
                    ShinobuPoiVaultBridge.PoiRulesBufferId,
                    DefaultRuleCapacity,
                    out NativeArray<PoiPlacementRuleDTO> rules) ||
                !TryReadPoiVaultBuffer(
                    vault,
                    ShinobuPoiVaultBridge.PoiBoundsBufferId,
                    DefaultBoundsCapacity,
                    out NativeArray<StructuralBoundsDTO> bounds) ||
                !TryResolveExistingPoiVaultBuffer(
                    vault,
                    ShinobuPoiVaultBridge.PoiTelemetryRingBufferId,
                    ShinobuPoiVaultBridge.BlackBoxFrameCount,
                    out NativeArray<PoiPlacementTelemetryEntry> telemetry))
            {
                return;
            }

            PoiOfflineBakeConfigDTO bakeConfig = config.Length > 0 ? config[0] : default;
            int candidateCount = math.clamp(bakeConfig.CandidateCount > 0 ? bakeConfig.CandidateCount : 512, 1, 8192);
            int transformCapacity = math.clamp(bakeConfig.MaxPoiTransforms > 0 ? bakeConfig.MaxPoiTransforms : 8192, 1, 50000);
            if (!TryEnsurePoiVaultBuffer(
                    vault,
                    ShinobuPoiVaultBridge.PoiCandidateAupsBufferId,
                    candidateCount,
                    NativeArrayOptions.UninitializedMemory,
                    out NativeArray<double3> candidates) ||
                !TryEnsurePoiVaultBuffer(
                    vault,
                    ShinobuPoiVaultBridge.PoiMockSignalsBufferId,
                    candidateCount,
                    NativeArrayOptions.UninitializedMemory,
                    out NativeArray<MockGeologySignal> signals) ||
                !TryEnsurePoiVaultBuffer(
                    vault,
                    ShinobuPoiVaultBridge.PoiTransformsBufferId,
                    transformCapacity,
                    NativeArrayOptions.UninitializedMemory,
                    out NativeArray<PoiTransformDTO> transforms) ||
                !TryEnsurePoiVaultBuffer(
                    vault,
                    ShinobuPoiVaultBridge.PoiVisualAnchorsBufferId,
                    candidateCount,
                    NativeArrayOptions.UninitializedMemory,
                    out NativeArray<VisualAnchorSampleDTO> anchors) ||
                !TryEnsurePoiVaultBuffer(
                    vault,
                    ShinobuPoiVaultBridge.PoiPlacementCountersBufferId,
                    4,
                    NativeArrayOptions.UninitializedMemory,
                    out NativeArray<int> counters))
            {
                return;
            }

            FillEditorCandidates(candidates, bakeConfig.Seed);
            for (int i = 0; i < counters.Length; i++)
                counters[i] = 0;

            MockGeologySignalJob signalJob = new MockGeologySignalJob
            {
                CandidateAups = candidates,
                Signals = signals,
                Seed = bakeConfig.Seed != 0u ? bakeConfig.Seed : 2166136261u,
                ForcedSlopeDegrees = -1f
            };

            PoiPlacementVaultArrayJob placementJob = new PoiPlacementVaultArrayJob
            {
                CandidateAups = candidates,
                Signals = signals,
                Rules = rules,
                Bounds = bounds,
                OutputTransforms = transforms,
                VisualAnchors = anchors,
                Counters = counters,
                TelemetryRing = telemetry,
                TelemetryIndex = 0u,
                Frame = (uint)math.max(0, (int)EditorApplication.timeSinceStartup),
                Seed = bakeConfig.Seed != 0u ? bakeConfig.Seed : 2166136261u,
                GlobalQualityWeight = math.saturate(_globalDensity),
                AnchorSampleStrideMeters = bakeConfig.AnchorSampleStrideMeters,
                MinimumAnchorScore = bakeConfig.MinimumAnchorScore,
                MaxSlopeDegreesOverride = _maxSlopeTolerance
            };

            PoiOfflineBakeFenceJob job = new PoiOfflineBakeFenceJob
            {
                Config = config,
                TelemetryRing = telemetry,
                TelemetryIndex = 1u,
                Frame = (uint)math.max(0, (int)EditorApplication.timeSinceStartup)
            };
            JobHandle signalHandle = ShinobuPoiJobGraph.ScheduleMockGeology(signalJob, candidateCount, _queuedBakeFenceHandle);
            JobHandle placementHandle = ShinobuPoiJobGraph.SchedulePlacementVaultArray(placementJob, signalHandle);
            _queuedBakeFenceHandle = ShinobuPoiJobGraph.ScheduleBakeFence(job, placementHandle);
            JobHandle.ScheduleBatchedJobs();
            _hasQueuedBakeFence = true;
            RefreshStatusLabels();
        }

        private void TryRetireQueuedBake()
        {
            if (!_hasQueuedBakeFence || !_queuedBakeFenceHandle.IsCompleted)
                return;

            _queuedBakeFenceHandle.Complete();
            _hasQueuedBakeFence = false;
            ReadPlacementCounters();
            RefreshStatusLabels();
            Repaint();
        }

        private bool HasPendingBake()
        {
            if (!_hasQueuedBakeFence)
                return false;

            if (_queuedBakeFenceHandle.IsCompleted)
            {
                TryRetireQueuedBake();
                return false;
            }

            return true;
        }

        private static void FillEditorCandidates(NativeArray<double3> candidates, uint seed)
        {
            if (!candidates.IsCreated || candidates.Length <= 0)
                return;

            int grid = math.max(1, (int)math.ceil(math.sqrt((float)candidates.Length)));
            double spacing = 180.0;
            double half = (grid - 1) * spacing * 0.5;
            float phase = ShinobuPoiMath.HashToUnit01(seed != 0u ? seed : 2166136261u) * math.PI * 2f;
            for (int i = 0; i < candidates.Length; i++)
            {
                int x = i % grid;
                int z = i / grid;
                float jitterX = math.sin((i + 1) * 12.9898f + phase) * 22f;
                float jitterZ = math.cos((i + 1) * 78.233f - phase) * 22f;
                candidates[i] = new double3(x * spacing - half + jitterX, -120.0, z * spacing - half + jitterZ);
            }
        }

        private void ReadPlacementCounters()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (!TryReadPoiVaultBuffer(
                    vault,
                    ShinobuPoiVaultBridge.PoiPlacementCountersBufferId,
                    2,
                    out NativeArray<int> counters))
            {
                return;
            }

            _lastGeneratedPoiCount = math.max(0, counters[0]);
            _lastAnchorCount = math.max(0, counters[1]);
        }

        private void OnSceneGui(SceneView sceneView)
        {
            if (!_drawGizmos || _hasQueuedBakeFence || Event.current.type != EventType.Repaint)
                return;

            OnDrawGizmos(sceneView);
        }

        private void OnDrawGizmos(SceneView sceneView)
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
                return;

            int poiCount = ResolvePlacementCounter(vault, 0, _previewLimit);
            int anchorCount = ResolvePlacementCounter(vault, 1, _previewLimit);

            if (TryReadPoiVaultBuffer(
                    vault,
                    ShinobuPoiVaultBridge.PoiTransformsBufferId,
                    1,
                    out NativeArray<PoiTransformDTO> poiTransforms))
            {
                DrawPoiMatrices(poiTransforms, poiCount);
            }

            if (TryReadPoiVaultBuffer(
                    vault,
                    ShinobuPoiVaultBridge.PoiVisualAnchorsBufferId,
                    1,
                    out NativeArray<VisualAnchorSampleDTO> anchors))
            {
                DrawAnchorHeatmap(anchors, anchorCount);
            }
        }

        private void DrawPoiMatrices(NativeArray<PoiTransformDTO> poiTransforms, int generatedCount)
        {
            int count = math.min(math.max(0, generatedCount), math.min(poiTransforms.Length, _previewLimit));
            Matrix4x4 previous = Handles.matrix;
            for (int i = 0; i < count; i++)
            {
                PoiTransformDTO dto = poiTransforms[i];
                Vector3 position = ToVector3(dto.AUP);
                if (dto.PrefabHash == ShinobuPoiConstants.PrefabHashTitaniumStilt)
                {
                    Handles.color = new Color(1f, 0.08f, 0.03f, 0.92f);
                    float halfHeight = math.max(0.25f, dto.Scale.y * 0.5f);
                    Handles.DrawLine(position + Vector3.up * halfHeight, position - Vector3.up * halfHeight, 3f);
                    continue;
                }

                Handles.color = dto.QuestNodeHash != 0u
                    ? new Color(0.2f, 0.95f, 1f, 0.95f)
                    : new Color(0.95f, 0.72f, 0.18f, 0.82f);
                Handles.matrix = Matrix4x4.TRS(position, ToQuaternion(dto.Rotation), ToVector3(dto.Scale));
                Handles.DrawWireCube(Vector3.zero, Vector3.one);
            }

            Handles.matrix = previous;
        }

        private void DrawAnchorHeatmap(NativeArray<VisualAnchorSampleDTO> anchors, int generatedCount)
        {
            int count = math.min(math.max(0, generatedCount), math.min(anchors.Length, _previewLimit));
            for (int i = 0; i < count; i++)
            {
                VisualAnchorSampleDTO anchor = anchors[i];
                float score = math.saturate(anchor.AnchorScore);
                Handles.color = Color.Lerp(new Color(1f, 0.05f, 0.02f, 0.24f), new Color(0.0f, 0.95f, 1f, 0.42f), score);
                Vector3 position = ToVector3(anchor.RootAup);
                Handles.DrawSolidDisc(position, Vector3.up, math.lerp(3f, 14f, score));
            }
        }

        private static int ResolvePlacementCounter(IDataVault vault, int counterIndex, int fallback)
        {
            if (TryReadPoiVaultBuffer(
                    vault,
                    ShinobuPoiVaultBridge.PoiPlacementCountersBufferId,
                    counterIndex + 1,
                    out NativeArray<int> counters) &&
                (uint)counterIndex < (uint)counters.Length)
            {
                return math.max(0, counters[counterIndex]);
            }

            return math.max(0, fallback);
        }

        private static Vector3 ToVector3(double3 value)
        {
            return new Vector3((float)value.x, (float)value.y, (float)value.z);
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        private static Quaternion ToQuaternion(quaternion value)
        {
            return new Quaternion(value.value.x, value.value.y, value.value.z, value.value.w);
        }
    }

    internal static class ShinobuPoiCsvRulesIngestor
    {
        public static unsafe int Parse(
            NativeArray<byte> csv,
            int byteCount,
            NativeArray<PoiPlacementRuleDTO> rules,
            NativeArray<StructuralBoundsDTO> bounds)
        {
            if (!csv.IsCreated || byteCount <= 0)
                return 0;

            int safeCount = math.min(byteCount, csv.Length);
            void* ptr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(csv);
            return Parse(new ReadOnlySpan<byte>(ptr, safeCount), rules, bounds);
        }

        public static int Parse(ReadOnlySpan<byte> csv, NativeArray<PoiPlacementRuleDTO> rules, NativeArray<StructuralBoundsDTO> bounds)
        {
            if (!rules.IsCreated || !bounds.IsCreated || csv.Length <= 0)
                return 0;

            int limit = math.min(rules.Length, bounds.Length);
            int count = 0;
            int lineStart = 0;
            for (int i = 0; i <= csv.Length && count < limit; i++)
            {
                bool end = i == csv.Length || csv[i] == (byte)'\n';
                if (!end)
                    continue;

                ReadOnlySpan<byte> line = Trim(csv.Slice(lineStart, i - lineStart));
                lineStart = i + 1;
                if (line.Length == 0 || IsHeader(line))
                    continue;

                Span<double> values = stackalloc double[12];
                int valueCount = ParseLine(line, values);
                if (valueCount < 10)
                    continue;

                float maxSlopeDegrees = (float)values[4];
                uint prefabHash = (uint)math.max(0.0, values[0]);
                uint biomeId = (uint)math.max(0.0, values[1]);
                uint questHash = valueCount > 10 ? (uint)math.max(0.0, values[10]) : 0u;
                int maxDebris = valueCount > 11 ? (int)math.max(0.0, values[11]) : 50;

                bounds[count] = new StructuralBoundsDTO
                {
                    Extents = new float3((float)values[7], (float)values[8], (float)values[9]),
                    CenterOffset = float3.zero,
                    ClearanceRadius = math.max((float)values[7], (float)values[9]),
                    _pad0 = 0u
                };

                rules[count] = new PoiPlacementRuleDTO
                {
                    PrefabHash = prefabHash != 0u ? prefabHash : ShinobuPoiConstants.PrefabHashRuinBase + (uint)count,
                    BiomeID = biomeId,
                    MinDepthMeters = (float)values[2],
                    MaxDepthMeters = (float)values[3],
                    MaxSlopeCos = math.cos(math.radians(math.clamp(maxSlopeDegrees, 0.1f, 89f))),
                    MinClusterSpacingMeters = 2000f,
                    ClusterRadiusMeters = (float)values[6],
                    BoundsIndex = count,
                    MaxDebrisMatrices = maxDebris,
                    StiltPrefabHash = ShinobuPoiConstants.PrefabHashTitaniumStilt,
                    DebrisPrefabHash = ShinobuPoiConstants.PrefabHashRustedPanel,
                    QuestNodeHash = questHash,
                    Flags = questHash != 0u ? ShinobuPoiConstants.FlagNarrative : 0u,
                    RuleHash = ShinobuPoiMath.MixHash(prefabHash, (uint)count),
                    _pad0 = 0u,
                    _pad1 = 0u
                };

                count++;
            }

            return count;
        }

        private static int ParseLine(ReadOnlySpan<byte> line, Span<double> values)
        {
            int count = 0;
            int cellStart = 0;
            for (int i = 0; i <= line.Length && count < values.Length; i++)
            {
                bool end = i == line.Length || line[i] == (byte)',' || line[i] == (byte)';';
                if (!end)
                    continue;

                ReadOnlySpan<byte> cell = Trim(line.Slice(cellStart, i - cellStart));
                cellStart = i + 1;
                if (TryParseNumber(cell, out double value))
                    values[count++] = value;
            }

            return count;
        }

        private static bool TryParseNumber(ReadOnlySpan<byte> cell, out double value)
        {
            value = 0.0;
            if (cell.Length == 0)
                return false;

            int index = 0;
            bool negative = false;
            if (cell[0] == (byte)'-')
            {
                negative = true;
                index = 1;
            }

            if (index + 1 < cell.Length && cell[index] == (byte)'0' && (cell[index + 1] == (byte)'x' || cell[index + 1] == (byte)'X'))
            {
                ulong hex = 0UL;
                for (int i = index + 2; i < cell.Length; i++)
                {
                    int digit = HexValue(cell[i]);
                    if (digit < 0)
                        return false;
                    hex = (hex << 4) | (uint)digit;
                }

                value = negative ? -(double)hex : hex;
                return true;
            }

            double integer = 0.0;
            double fraction = 0.0;
            double divisor = 1.0;
            bool seenDigit = false;
            bool afterDecimal = false;
            for (int i = index; i < cell.Length; i++)
            {
                byte c = cell[i];
                if (c == (byte)'.')
                {
                    if (afterDecimal)
                        return false;
                    afterDecimal = true;
                    continue;
                }

                if (c < (byte)'0' || c > (byte)'9')
                    return false;

                seenDigit = true;
                int digit = c - (byte)'0';
                if (afterDecimal)
                {
                    divisor *= 10.0;
                    fraction += digit / divisor;
                }
                else
                {
                    integer = integer * 10.0 + digit;
                }
            }

            if (!seenDigit)
                return false;

            value = negative ? -(integer + fraction) : integer + fraction;
            return true;
        }

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> span)
        {
            int start = 0;
            int end = span.Length - 1;
            while (start <= end && IsWhitespace(span[start]))
                start++;
            while (end >= start && IsWhitespace(span[end]))
                end--;
            return start > end ? ReadOnlySpan<byte>.Empty : span.Slice(start, end - start + 1);
        }

        private static bool IsWhitespace(byte c)
        {
            return c == (byte)' ' || c == (byte)'\t' || c == (byte)'\r';
        }

        private static bool IsHeader(ReadOnlySpan<byte> line)
        {
            byte c = line[0];
            return !((c >= (byte)'0' && c <= (byte)'9') || c == (byte)'-');
        }

        private static int HexValue(byte c)
        {
            if (c >= (byte)'0' && c <= (byte)'9')
                return c - (byte)'0';
            if (c >= (byte)'a' && c <= (byte)'f')
                return c - (byte)'a' + 10;
            if (c >= (byte)'A' && c <= (byte)'F')
                return c - (byte)'A' + 10;
            return -1;
        }
    }
}
#endif
