#if UNITY_EDITOR
using Hecton8.AI.Ecosystem;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Editor
{
    public sealed class BiomassBoidTunerWindow : EditorWindow
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

        private IMGUIContainer _imguiContainer;

        [MenuItem("HECTON-8/Abyssal Swarm Tuner")]
        public static void OpenAbyssal()
        {
            GetWindow<BiomassBoidTunerWindow>("Abyssal Swarm Tuner");
        }

        [MenuItem("HECTON-8/Biomass & Boid Tuner")]
        public static void Open()
        {
            OpenAbyssal();
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
            if (!Application.isPlaying || vault == null)
            {
                EditorGUILayout.HelpBox("Play Mode DataVault is not available.", MessageType.Info);
                return;
            }

            if (!vault.TryGetBufferHandle(BufferID.ShinobuEcosystemTuning, out VaultBufferHandle<ShinobuEcosystemTuning> tuningHandle) ||
                !tuningHandle.IsCreated)
            {
                EditorGUILayout.HelpBox("SHINOBU tuning buffer is not registered.", MessageType.Warning);
                return;
            }

            ref ShinobuEcosystemTuning tuning = ref tuningHandle.GetElementAsRef(vault, 0);
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
                tuning = ShinobuEcosystemTuning.Sanitize(next);
                Repaint();
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space(8f);
            DrawCounters(vault);
            DrawTelemetry(vault);
        }

        private static void DrawCounters(IDataVault vault)
        {
            if (!vault.TryGetBuffer<int>(BufferID.ShinobuEcosystemCounters, out var counters) || !counters.IsCreated)
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
            if (!vault.TryGetBuffer<ShinobuTelemetryEntry>(BufferID.ShinobuEcosystemTelemetryRing, out NativeArray<ShinobuTelemetryEntry> ring) ||
                !ring.IsCreated ||
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

            if (!vault.TryGetBuffer<ShinobuEcosystemTuning>(BufferID.ShinobuEcosystemTuning, out var tuningArray) ||
                !tuningArray.IsCreated ||
                tuningArray.Length <= 0)
            {
                return;
            }

            uint flags = tuningArray[0].Flags;
            if ((flags & ShinobuEcosystemBalancer.TuningFlagEditorDebugGrid) != 0u)
                DrawHashGrid(vault);

            if ((flags & ShinobuEcosystemBalancer.TuningFlagEditorDebugVectors) != 0u)
                DrawVectorField(vault, sceneView);
        }

        private static void DrawHashGrid(IDataVault vault)
        {
            if (!vault.TryGetBuffer<ShinobuSpatialHashDebugCell>(BufferID.ShinobuSpatialHashDebugCells, out var cells) ||
                !cells.IsCreated ||
                !vault.TryGetBuffer<int>(BufferID.ShinobuEcosystemCounters, out var counters) ||
                !counters.IsCreated)
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
                        float3 flow = global::CurrentManager.SampleCurrent(p, Time.realtimeSinceStartup, 0.015f, 0.12f, 2.0f, 0.2f);
                        Vector3 start = new Vector3(p.x, p.y, p.z);
                        Vector3 end = start + new Vector3(flow.x, flow.y, flow.z);
                        Handles.DrawLine(start, end, 1.5f);
                        drawn++;
                    }
                }
            }

            if (!vault.TryGetBuffer<AmbientEntityDTO>(BufferID.ShinobuAmbientEntities, out var entities) ||
                !vault.TryGetBuffer<AmbientEntityAupDTO>(BufferID.ShinobuAmbientAups, out var aups) ||
                !entities.IsCreated ||
                !aups.IsCreated)
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
    }
}
#endif
