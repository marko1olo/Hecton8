#if UNITY_EDITOR
using Hecton8.AI.Ecosystem;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

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

        [MenuItem("HECTON-8/Biomass & Boid Tuner")]
        public static void Open()
        {
            GetWindow<BiomassBoidTunerWindow>("Biomass & Boid Tuner");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += DrawHashGridSceneView;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawHashGridSceneView;
        }

        private void OnGUI()
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
            bool drawGrid = (next.Flags & ShinobuEcosystemBalancer.TuningFlagEditorDebugGrid) != 0u;
            drawGrid = EditorGUILayout.Toggle("Draw Spatial Hash Grid", drawGrid);
            if (drawGrid)
                next.Flags |= ShinobuEcosystemBalancer.TuningFlagEditorDebugGrid;
            else
                next.Flags &= ~ShinobuEcosystemBalancer.TuningFlagEditorDebugGrid;

            if (EditorGUI.EndChangeCheck())
            {
                tuning = ShinobuEcosystemTuning.Sanitize(next);
                Repaint();
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space(8f);
            DrawCounters(vault);
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
                tuningArray.Length <= 0 ||
                (tuningArray[0].Flags & ShinobuEcosystemBalancer.TuningFlagEditorDebugGrid) == 0u)
            {
                return;
            }

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
    }
}
#endif
