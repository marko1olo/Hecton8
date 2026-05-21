#if UNITY_EDITOR
using System.IO;
using Hecton8.AI.Ecosystem;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Editor
{
    public sealed class AbyssalSwarmTunerWindow : EditorWindow
    {
        private const int CounterActive = 1;
        private const int CounterHydrated = 2;
        private const int CounterFree = 3;
        private const int CounterDehydratedSectors = 4;
        private const int CounterSkipped = 5;
        private const int CounterInvalidMath = 6;
        private const int CounterDebugCellCount = 8;
        private const uint EntityFlagHydrated = 1u << 2;
        private const int TelemetryGraphHeight = 46;
        private const int MaxVectorFieldSamples = 75;
        private const int MaxBoidVectorSamples = 128;
        private const int CsvMaxBytes = 8192;
        private const string TuningCsvPrimary = "Data/Precomputed/ecosystem_balance.csv";
        private const string TuningCsvFallback = "ecosystem_balance.csv";
        private const string SpeciesCsvPrimary = "Data/Precomputed/swarm_species_profiles.csv";
        private const string SpeciesCsvFallback = "swarm_species_profiles.csv";

        private IMGUIContainer _imguiContainer;

        [MenuItem("HECTON-8/Abyssal Swarm Tuner")]
        public static void OpenAbyssal()
        {
            GetWindow<AbyssalSwarmTunerWindow>("Abyssal Swarm Tuner");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += DrawHashGridSceneView;
        }

        private void CreateGUI()
        {
            rootVisualElement.Clear();
            _imguiContainer = new IMGUIContainer(DrawWindowIMGUI);
            rootVisualElement.Add(_imguiContainer);
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawHashGridSceneView;
        }

        private void OnGUI()
        {
            if (_imguiContainer == null)
                DrawWindowIMGUI();
        }

        private void DrawWindowIMGUI()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            DrawAuthoringBridge(vault);

            if (!Application.isPlaying || vault == null)
            {
                EditorGUILayout.HelpBox("Play Mode DataVault is not available.", MessageType.Info);
                return;
            }

            if (!TryReadFirst(vault, BufferID.ShinobuEcosystemTuning, out ShinobuEcosystemTuning tuning))
            {
                EditorGUILayout.HelpBox("SHINOBU tuning buffer is not registered.", MessageType.Warning);
                return;
            }

            ShinobuEcosystemTuning next = tuning;
            EditorGUI.BeginChangeCheck();
            next.SeparationWeight = EditorGUILayout.Slider("Separation Weight", next.SeparationWeight, 0.05f, 8f);
            next.AlignmentWeight = EditorGUILayout.Slider("Alignment Weight", next.AlignmentWeight, 0.01f, 4f);
            next.CohesionWeight = EditorGUILayout.Slider("Cohesion Weight", next.CohesionWeight, 0.01f, 4f);
            next.PredatorAvoidanceWeight = EditorGUILayout.Slider("Predator Avoidance", next.PredatorAvoidanceWeight, 0.1f, 24f);
            next.HerbivoreBirthRate = EditorGUILayout.Slider("Herbivore Birth Rate", next.HerbivoreBirthRate, 0.001f, 0.5f);
            next.CarnivoreBirthRate = EditorGUILayout.Slider("Carnivore Birth Rate", next.CarnivoreBirthRate, 0.001f, 0.25f);
            next.FloraGrowthRate = EditorGUILayout.Slider("Flora Growth Rate", next.FloraGrowthRate, 0.001f, 1f);
            next.FeedRate = EditorGUILayout.Slider("Feed Rate", next.FeedRate, 0.001f, 0.2f);
            next.BiomassReproductionThreshold = EditorGUILayout.Slider("Reproduction Biomass", next.BiomassReproductionThreshold, 0.25f, 8f);
            next.MaxSpeedMetersPerSecond = EditorGUILayout.Slider("Max Speed", next.MaxSpeedMetersPerSecond, 0.25f, 16f);
            next.CarryingCapacity = EditorGUILayout.Slider("Carrying Capacity", next.CarryingCapacity, 250f, 50000f);
            next.PredationRate = EditorGUILayout.Slider("Predation Rate", next.PredationRate, 0.00001f, 0.001f);
            bool drawGrid = (next.Flags & ShinobuEcosystemBalancer.TuningFlagEditorDebugGrid) != 0u;
            drawGrid = EditorGUILayout.Toggle("Draw Spatial Hash Grid", drawGrid);
            if (drawGrid)
                next.Flags |= ShinobuEcosystemBalancer.TuningFlagEditorDebugGrid;
            else
                next.Flags &= ~ShinobuEcosystemBalancer.TuningFlagEditorDebugGrid;
            bool drawVectors = (next.Flags & ShinobuEcosystemBalancer.TuningFlagEditorDebugVectors) != 0u;
            drawVectors = EditorGUILayout.Toggle("Draw Flow Vectors", drawVectors);
            if (drawVectors)
                next.Flags |= ShinobuEcosystemBalancer.TuningFlagEditorDebugVectors;
            else
                next.Flags &= ~ShinobuEcosystemBalancer.TuningFlagEditorDebugVectors;

            if (EditorGUI.EndChangeCheck())
            {
                if (TryWriteFirst(vault, BufferID.ShinobuEcosystemTuning, ShinobuEcosystemTuning.Sanitize(next)))
                {
                    Repaint();
                    SceneView.RepaintAll();
                }
            }

            EditorGUILayout.Space(8f);
            DrawCounters(vault);
            DrawTelemetry(vault);
        }

        private void DrawAuthoringBridge(IDataVault vault)
        {
            EditorGUILayout.LabelField("Designer Bridge", EditorStyles.boldLabel);
            DrawCsvBridgeRow("Tuning CSV", TuningCsvPrimary, TuningCsvFallback);
            DrawCsvBridgeRow("Species CSV", SpeciesCsvPrimary, SpeciesCsvFallback);
            DrawRuntimeBridgeState(vault);
            DrawLayoutSummary();

            using (new EditorGUI.DisabledScope(!Application.isPlaying || vault == null))
            {
                if (GUILayout.Button("Force CSV -> Vault Reload"))
                {
                    ShinobuEcosystemBalancer.EnsureRuntimeService().ForceDesignerDataReload();
                    Repaint();
                    SceneView.RepaintAll();
                }
            }

            EditorGUILayout.Space(8f);
        }

        private static void DrawCsvBridgeRow(string label, string primaryRelativePath, string fallbackRelativePath)
        {
            string path = ResolveCsvPath(primaryRelativePath, fallbackRelativePath);
            bool exists = File.Exists(path);
            long byteCount = exists ? new FileInfo(path).Length : 0L;
            int rowCount = exists ? CountNonEmptyCsvRows(path, CsvMaxBytes) : 0;
            uint hash = exists ? ComputeFnv1aFileHash(path, CsvMaxBytes) : 0u;

            EditorGUILayout.LabelField(label + " Source", path);
            EditorGUILayout.LabelField(label + " Rows/Bytes", rowCount.ToString() + " rows / " + byteCount.ToString() + " bytes");
            EditorGUILayout.LabelField(label + " Checksum", exists ? "FNV1A32 0x" + hash.ToString("X8") : "MISSING");

            if (!exists)
            {
                EditorGUILayout.HelpBox(label + " is missing; runtime will keep deterministic fallback/mock data.", MessageType.Warning);
                return;
            }

            if (byteCount > CsvMaxBytes)
                EditorGUILayout.HelpBox(label + " exceeds SHINOBU CSV scratch capacity.", MessageType.Error);
        }

        private static void DrawRuntimeBridgeState(IDataVault vault)
        {
            int tuningRows = 0;
            int speciesRows = 0;
            if (vault != null)
            {
                if (TryReadExistingVaultView(vault, BufferID.ShinobuEcosystemTuning, out NativeArray<ShinobuEcosystemTuning> tuning))
                    tuningRows = tuning.Length;

                if (TryReadExistingVaultView(vault, BufferID.ShinobuSwarmSpeciesProfiles, out NativeArray<SwarmSpeciesProfileDTO> profiles))
                {
                    for (int i = 0; i < profiles.Length; i++)
                    {
                        if (profiles[i].BiomassHash != 0u)
                            speciesRows++;
                    }
                }
            }

            EditorGUILayout.LabelField("Schema", "SHINOBU_105_CSV_V1 / FNV1A32 keys / cold reload only");
            EditorGUILayout.LabelField("Binary Output", "GlobalDataVault: ShinobuEcosystemTuning, ShinobuSwarmSpeciesProfiles");
            EditorGUILayout.LabelField("Vault Rows", "Tuning=" + tuningRows.ToString() + " Species=" + speciesRows.ToString());
            EditorGUILayout.LabelField("Validation", vault != null ? "DataVault visible" : "DataVault unavailable");
        }

        private static void DrawLayoutSummary()
        {
            int boidStateSize = UnsafeUtility.SizeOf<BoidStateDTO>();
            int targetSize = UnsafeUtility.SizeOf<BoidTargetDTO>();
            int matrixSize = UnsafeUtility.SizeOf<BoidMatrixDTO>();
            int argsSize = UnsafeUtility.SizeOf<BoidIndirectArgsDTO>();
            int tuningSize = UnsafeUtility.SizeOf<ShinobuEcosystemTuning>();
            int speciesSize = UnsafeUtility.SizeOf<SwarmSpeciesProfileDTO>();

            EditorGUILayout.LabelField("BoidStateDTO", "32B expected: AUP@0 SpeciesID@24 PackIndex@26 Speed@28; observed " + boidStateSize.ToString() + "B");
            EditorGUILayout.LabelField("GPU DTOs", "BoidTarget=" + targetSize.ToString() + "B Matrix=" + matrixSize.ToString() + "B Args=" + argsSize.ToString() + "B");
            EditorGUILayout.LabelField("Tuning DTOs", "Tuning=" + tuningSize.ToString() + "B SpeciesProfile=" + speciesSize.ToString() + "B");

            bool aligned = (boidStateSize == 32) &&
                           (targetSize == 32) &&
                           (matrixSize == 64) &&
                           (argsSize == 16) &&
                           (tuningSize == 64) &&
                           (speciesSize == 32);
            if (!aligned)
                EditorGUILayout.HelpBox("ARM64 layout warning: SHINOBU DTO size drift detected.", MessageType.Error);
        }

        private static string ResolveCsvPath(string primaryRelativePath, string fallbackRelativePath)
        {
            string root = ResolveProjectRoot();
            string primary = Path.Combine(root, primaryRelativePath);
            if (File.Exists(primary))
                return primary;

            string fallback = Path.Combine(root, fallbackRelativePath);
            return File.Exists(fallback) ? fallback : primary;
        }

        private static string ResolveProjectRoot()
        {
            string assetsPath = Application.dataPath;
            DirectoryInfo parent = Directory.GetParent(assetsPath);
            return parent != null ? parent.FullName : assetsPath;
        }

        private static int CountNonEmptyCsvRows(string path, int limitBytes)
        {
            int rows = 0;
            bool hasContent = false;
            int read = 0;
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                while (read < limitBytes)
                {
                    int value = stream.ReadByte();
                    if (value < 0)
                        break;

                    read++;
                    if (value == '\n')
                    {
                        if (hasContent)
                            rows++;
                        hasContent = false;
                    }
                    else if (value != '\r' && value != ',' && value != ' ' && value != '\t')
                    {
                        hasContent = true;
                    }
                }
            }

            if (hasContent)
                rows++;

            return math.max(0, rows - 1);
        }

        private static uint ComputeFnv1aFileHash(string path, int limitBytes)
        {
            uint hash = 2166136261u;
            int read = 0;
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                while (read < limitBytes)
                {
                    int value = stream.ReadByte();
                    if (value < 0)
                        break;

                    read++;
                    unchecked
                    {
                        hash = (hash ^ (byte)value) * 16777619u;
                    }
                }
            }

            return hash;
        }

        private static void DrawCounters(IDataVault vault)
        {
            if (!TryReadExistingVaultView(vault, BufferID.ShinobuEcosystemCounters, out NativeArray<int> counters))
                return;

            EditorGUILayout.LabelField("Active", ReadCounter(counters, CounterActive).ToString());
            EditorGUILayout.LabelField("Hydrated", ReadCounter(counters, CounterHydrated).ToString());
            EditorGUILayout.LabelField("Free Slots", ReadCounter(counters, CounterFree).ToString());
            EditorGUILayout.LabelField("Dehydrated Sectors", ReadCounter(counters, CounterDehydratedSectors).ToString());
            EditorGUILayout.LabelField("Skipped", ReadCounter(counters, CounterSkipped).ToString());
            EditorGUILayout.LabelField("Invalid Math", ReadCounter(counters, CounterInvalidMath).ToString());
        }

        private static void DrawTelemetry(IDataVault vault)
        {
            if (!TryReadExistingVaultView(vault, BufferID.ShinobuEcosystemTelemetryRing, out NativeArray<ShinobuTelemetryEntry> ring) ||
                ring.Length <= 0)
            {
                return;
            }

            ShinobuTelemetryEntry latest = default;
            float maxMs = 0.001f;
            int sampleCount = math.min(ring.Length, 300);
            for (int i = 0; i < sampleCount; i++)
            {
                ShinobuTelemetryEntry entry = ring[i];
                if (entry.Frame >= latest.Frame)
                    latest = entry;
                maxMs = math.max(maxMs, entry.FlockingSolveTimeMs);
            }

            EditorGUILayout.LabelField("Quality Weight", latest.GlobalQualityWeight.ToString("0.000"));
            EditorGUILayout.LabelField("Solve ms", latest.FlockingSolveTimeMs.ToString("0.000"));
            EditorGUILayout.LabelField("Budget", latest.ActiveBoidCount.ToString());

            Rect rect = GUILayoutUtility.GetRect(1f, TelemetryGraphHeight);
            EditorGUI.DrawRect(rect, new Color(0.05f, 0.07f, 0.08f, 1f));
            float width = math.max(1f, rect.width);
            for (int i = 0; i < sampleCount; i++)
            {
                ShinobuTelemetryEntry entry = ring[i];
                float x = rect.x + (i / (float)sampleCount) * width;
                float h = math.saturate(entry.FlockingSolveTimeMs / maxMs) * rect.height;
                Rect bar = new Rect(x, rect.yMax - h, math.max(1f, width / sampleCount), h);
                Color color = (entry.Flags & ShinobuEcosystemBalancer.TelemetryFlagSolveOverBudget) != 0u
                    ? new Color(1f, 0.23f, 0.12f, 0.95f)
                    : new Color(0.18f, 0.75f, 0.86f, 0.9f);
                EditorGUI.DrawRect(bar, color);
            }
        }

        private static int ReadCounter(Unity.Collections.NativeArray<int> counters, int index)
        {
            return (uint)index < (uint)counters.Length ? counters[index] : 0;
        }

        private static void DrawHashGridSceneView(SceneView sceneView)
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (!Application.isPlaying || vault == null)
                return;

            if (!TryReadFirst(vault, BufferID.ShinobuEcosystemTuning, out ShinobuEcosystemTuning tuning))
            {
                return;
            }

            uint flags = tuning.Flags;
            if ((flags & ShinobuEcosystemBalancer.TuningFlagEditorDebugGrid) != 0u)
                DrawHashGrid(vault);

            if ((flags & ShinobuEcosystemBalancer.TuningFlagEditorDebugVectors) != 0u)
                DrawVectorField(vault, sceneView);
        }

        private static void DrawHashGrid(IDataVault vault)
        {
            if (!TryReadExistingVaultView(vault, BufferID.ShinobuSpatialHashDebugCells, out NativeArray<ShinobuSpatialHashDebugCell> cells) ||
                !TryReadExistingVaultView(vault, BufferID.ShinobuEcosystemCounters, out NativeArray<int> counters))
            {
                return;
            }

            int count = math.clamp(ReadCounter(counters, CounterDebugCellCount), 0, cells.Length);
            for (int i = 0; i < count; i++)
            {
                ShinobuSpatialHashDebugCell cell = cells[i];
                if ((cell.Flags & 1u) == 0u)
                    continue;

                float occupancy01 = math.saturate(cell.Occupancy / 32f);
                Handles.color = Color.Lerp(Color.green, Color.red, occupancy01);
                Vector3 center = new Vector3(cell.CenterLocal.x, cell.CenterLocal.y, cell.CenterLocal.z);
                float size = math.max(0.25f, cell.CellSizeMeters);
                Handles.DrawWireCube(center, Vector3.one * size);
            }
        }

        private static void DrawVectorField(IDataVault vault, SceneView sceneView)
        {
            Camera camera = sceneView.camera;
            Vector3 origin = camera != null ? camera.transform.position : Vector3.zero;
            Handles.color = new Color(0.1f, 0.82f, 0.96f, 0.82f);
            int drawn = 0;
            for (int x = -2; x <= 2 && drawn < MaxVectorFieldSamples; x++)
            {
                for (int y = -1; y <= 1 && drawn < MaxVectorFieldSamples; y++)
                {
                    for (int z = -2; z <= 2 && drawn < MaxVectorFieldSamples; z++)
                    {
                        float3 p = new float3(origin.x + x * 8f, origin.y + y * 5f, origin.z + z * 8f);
                        float previewPhase = math.frac(math.dot(p, new float3(0.03125f, 0f, 0.02173913f))) * 31.415926f;
                        float3 flow = global::CurrentManager.SampleCurrent(p, previewPhase, 0.015f, 0.12f, 2.0f, 0.2f);
                        Vector3 start = new Vector3(p.x, p.y, p.z);
                        Vector3 end = start + new Vector3(flow.x, flow.y, flow.z);
                        Handles.DrawLine(start, end, 1.5f);
                        drawn++;
                    }
                }
            }

            if (!TryReadExistingVaultView(vault, BufferID.ShinobuAmbientEntities, out NativeArray<AmbientEntityDTO> entities) ||
                !TryReadExistingVaultView(vault, BufferID.ShinobuAmbientAups, out NativeArray<AmbientEntityAupDTO> aups))
            {
                return;
            }

            int count = math.min(entities.Length, aups.Length);
            int stride = math.max(1, count / MaxBoidVectorSamples);
            Handles.color = new Color(1f, 0.86f, 0.25f, 0.86f);
            for (int i = 0; i < count; i += stride)
            {
                AmbientEntityAupDTO meta = aups[i];
                if ((meta.Flags & EntityFlagHydrated) == 0u)
                    continue;

                AmbientEntityDTO entity = entities[i];
                float3 velocity = entity.Velocity;
                float lenSq = math.lengthsq(velocity);
                if (!math.isfinite(lenSq) || lenSq <= 0.0001f)
                    continue;

                float3 direction = velocity * math.rsqrt(lenSq);
                Vector3 start = new Vector3(entity.Position.x, entity.Position.y, entity.Position.z);
                Vector3 end = start + new Vector3(direction.x, direction.y, direction.z) * 2.5f;
                Handles.DrawLine(start, end, 2f);
            }
        }

        private static bool TryReadFirst<T>(IDataVault vault, BufferID bufferId, out T value)
            where T : struct
        {
            value = default;
            if (!TryReadExistingVaultView(vault, bufferId, out NativeArray<T> buffer) || buffer.Length <= 0)
                return false;

            value = buffer[0];
            return true;
        }

        private static bool TryWriteFirst<T>(IDataVault vault, BufferID bufferId, in T value)
            where T : struct
        {
            if (vault == null ||
                !vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> handle) ||
                !vault.TryAcquireWriteLock(in handle, SystemID.CoreDiagnostics, out NativeArray<T> buffer))
            {
                return false;
            }

            try
            {
                if (!buffer.IsCreated || buffer.Length <= 0)
                    return false;

                buffer[0] = value;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, SystemID.CoreDiagnostics);
            }
        }

        private static bool TryReadExistingVaultView<T>(IDataVault vault, BufferID bufferId, out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            return vault != null &&
                   vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> handle) &&
                   vault.TryReadHandle(in handle, out buffer) &&
                   buffer.IsCreated;
        }
    }
}
#endif
