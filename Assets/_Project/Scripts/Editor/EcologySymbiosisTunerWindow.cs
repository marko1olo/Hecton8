#if UNITY_EDITOR
using Hecton8.AI.Ecosystem;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    public sealed class EcologySymbiosisTunerWindow : EditorWindow
    {
        private const int MaxGizmoLines = 128;

        [MenuItem("HECTON-8/Ecology Symbiosis Tuner")]
        public static void Open()
        {
            GetWindow<EcologySymbiosisTunerWindow>("Ecology Symbiosis Tuner");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnDrawGizmos;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnDrawGizmos;
        }

        private void OnGUI()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (!Application.isPlaying || vault == null)
            {
                EditorGUILayout.HelpBox("Play Mode DataVault is not available.", MessageType.Info);
                return;
            }

            if (!vault.TryGetBufferHandle(BufferID.ShinobuSymbiosisTuning, out VaultBufferHandle<SymbiosisTuningDTO> tuningHandle) ||
                !tuningHandle.IsCreated)
            {
                EditorGUILayout.HelpBox("Symbiosis tuning buffer is not registered.", MessageType.Warning);
                return;
            }

            ref SymbiosisTuningDTO tuning = ref tuningHandle.GetElementAsRef(vault, 0);
            SymbiosisTuningDTO next = SymbiosisTuningDTO.Sanitize(tuning);
            EditorGUI.BeginChangeCheck();
            next.FeedingRate = EditorGUILayout.Slider("Feeding Rate", next.FeedingRate, 0.001f, 0.25f);
            next.ToxinPotency = EditorGUILayout.Slider("Toxin Potency", next.ToxinPotency, 0.01f, 4f);
            next.CamouflageRadius = EditorGUILayout.Slider("Camouflage Radius", next.CamouflageRadius, 0.1f, 12f);
            next.ParasiteGrowthSpeed = EditorGUILayout.Slider("Parasite Growth Speed", next.ParasiteGrowthSpeed, 0.0005f, 0.2f);
            next.OxygenRateScale = EditorGUILayout.Slider("Oxygen Rate Scale", next.OxygenRateScale, 0.001f, 0.4f);
            next.MacroThreshold = EditorGUILayout.Slider("Macro Threshold", next.MacroThreshold, 0.05f, 0.8f);
            bool drawGizmos = (next.Flags & ShinobuFloraFaunaSymbiosisSolver.TuningFlagEditorGizmos) != 0u;
            drawGizmos = EditorGUILayout.Toggle("Draw Symbiosis Lines", drawGizmos);
            if (drawGizmos)
                next.Flags |= ShinobuFloraFaunaSymbiosisSolver.TuningFlagEditorGizmos;
            else
                next.Flags &= ~ShinobuFloraFaunaSymbiosisSolver.TuningFlagEditorGizmos;

            if (EditorGUI.EndChangeCheck())
            {
                tuning = SymbiosisTuningDTO.Sanitize(next);
                Repaint();
                SceneView.RepaintAll();
            }

            DrawCounters(vault);
        }

        private static void DrawCounters(IDataVault vault)
        {
            if (!vault.TryGetBufferHandle(BufferID.ShinobuSymbiosisCounters, out VaultBufferHandle<SymbiosisCounterDTO> counterHandle) ||
                !counterHandle.IsCreated)
            {
                return;
            }

            ref readonly SymbiosisCounterDTO counter = ref counterHandle.GetElementAsReadOnlyRef(vault, 0);
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Active Exchanges", counter.ActiveExchanges.ToString());
            EditorGUILayout.LabelField("Biomass Transferred", (counter.BiomassTransferredMilli * 0.001f).ToString("0.000"));
            EditorGUILayout.LabelField("Oxygen Emitters", counter.OxygenEmitterCount.ToString());
            EditorGUILayout.LabelField("Toxemia", counter.ToxemiaCount.ToString());
            EditorGUILayout.LabelField("Camouflage", counter.CamouflageCount.ToString());
            EditorGUILayout.LabelField("Seeds", counter.SeedCount.ToString());
            EditorGUILayout.LabelField("Adherence", counter.AdherenceCount.ToString());
            EditorGUILayout.LabelField("Overflow", counter.OverflowCount.ToString());
        }

        private static void OnDrawGizmos(SceneView sceneView)
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (!Application.isPlaying || vault == null)
                return;

            if (!vault.TryGetBufferHandle(BufferID.ShinobuSymbiosisTuning, out VaultBufferHandle<SymbiosisTuningDTO> tuningHandle) ||
                !tuningHandle.IsCreated)
            {
                return;
            }

            ref readonly SymbiosisTuningDTO tuning = ref tuningHandle.GetElementAsReadOnlyRef(vault, 0);
            if ((tuning.Flags & ShinobuFloraFaunaSymbiosisSolver.TuningFlagEditorGizmos) == 0u)
                return;

            if (!TryResolveGizmoBuffers(
                    vault,
                    out NativeArray<SymbiosisExchangeDTO> exchanges,
                    out NativeArray<SymbiosisCounterDTO> counters,
                    out NativeArray<SymbiosisFloraDTO> flora,
                    out NativeArray<SymbiosisFloraAupDTO> floraAups,
                    out NativeArray<MockFishSymbiosisDTO> mockFish,
                    out NativeArray<AmbientEntityAupDTO> ambientAups))
            {
                return;
            }

            int count = math.min(math.min(counters[0].ActiveExchanges, exchanges.Length), MaxGizmoLines);
            Handles.color = new Color(0.1f, 1f, 0.35f, 0.9f);
            for (int i = 0; i < count; i++)
            {
                SymbiosisExchangeDTO exchange = exchanges[i];
                if (!TryFindFloraAup(exchange.FloraHash, flora, floraAups, out AbsoluteUniversePosition floraAup) ||
                    !TryFindFaunaAup(exchange.FaunaHash, mockFish, ambientAups, out AbsoluteUniversePosition faunaAup))
                {
                    continue;
                }

                Handles.DrawLine(ToRuntimeVector3(in floraAup), ToRuntimeVector3(in faunaAup), 2f);
            }
        }

        private static bool TryResolveGizmoBuffers(
            IDataVault vault,
            out NativeArray<SymbiosisExchangeDTO> exchanges,
            out NativeArray<SymbiosisCounterDTO> counters,
            out NativeArray<SymbiosisFloraDTO> flora,
            out NativeArray<SymbiosisFloraAupDTO> floraAups,
            out NativeArray<MockFishSymbiosisDTO> mockFish,
            out NativeArray<AmbientEntityAupDTO> ambientAups)
        {
            exchanges = default;
            counters = default;
            flora = default;
            floraAups = default;
            mockFish = default;
            ambientAups = default;

            if (!vault.TryGetBufferHandle(BufferID.ShinobuSymbiosisExchanges, out VaultBufferHandle<SymbiosisExchangeDTO> exchangeHandle) ||
                !vault.TryGetBufferHandle(BufferID.ShinobuSymbiosisCounters, out VaultBufferHandle<SymbiosisCounterDTO> counterHandle) ||
                !vault.TryGetBufferHandle(BufferID.ShinobuSymbiosisFlora, out VaultBufferHandle<SymbiosisFloraDTO> floraHandle) ||
                !vault.TryGetBufferHandle(BufferID.ShinobuSymbiosisFloraAups, out VaultBufferHandle<SymbiosisFloraAupDTO> floraAupHandle) ||
                !vault.TryGetBufferHandle(BufferID.ShinobuSymbiosisMockFish, out VaultBufferHandle<MockFishSymbiosisDTO> mockFishHandle) ||
                !vault.TryGetBufferHandle(BufferID.ShinobuAmbientAups, out VaultBufferHandle<AmbientEntityAupDTO> ambientAupHandle))
            {
                return false;
            }

            exchanges = exchangeHandle.Resolve(vault);
            counters = counterHandle.Resolve(vault);
            flora = floraHandle.Resolve(vault);
            floraAups = floraAupHandle.Resolve(vault);
            mockFish = mockFishHandle.Resolve(vault);
            ambientAups = ambientAupHandle.Resolve(vault);
            return exchanges.IsCreated &&
                   counters.IsCreated &&
                   counters.Length > 0 &&
                   flora.IsCreated &&
                   floraAups.IsCreated &&
                   mockFish.IsCreated &&
                   ambientAups.IsCreated;
        }

        private static bool TryFindFloraAup(
            uint floraHash,
            NativeArray<SymbiosisFloraDTO> flora,
            NativeArray<SymbiosisFloraAupDTO> floraAups,
            out AbsoluteUniversePosition aup)
        {
            int count = math.min(flora.Length, floraAups.Length);
            for (int i = 0; i < count; i++)
            {
                if (flora[i].FloraHash == floraHash)
                {
                    aup = floraAups[i].PositionAup.ToAup();
                    return true;
                }
            }

            aup = default;
            return false;
        }

        private static bool TryFindFaunaAup(
            uint faunaHash,
            NativeArray<MockFishSymbiosisDTO> mockFish,
            NativeArray<AmbientEntityAupDTO> ambientAups,
            out AbsoluteUniversePosition aup)
        {
            for (int i = 0; i < mockFish.Length; i++)
            {
                if (mockFish[i].StableSeed == faunaHash)
                {
                    aup = mockFish[i].PositionAup.ToAup();
                    return true;
                }
            }

            for (int i = 0; i < ambientAups.Length; i++)
            {
                if (ambientAups[i].StableSeed == faunaHash)
                {
                    aup = ambientAups[i].PositionAup;
                    return true;
                }
            }

            aup = default;
            return false;
        }

        private static Vector3 ToRuntimeVector3(in AbsoluteUniversePosition aup)
        {
            float3 runtime = aup.ToRuntimeFloat3();
            return new Vector3(runtime.x, runtime.y, runtime.z);
        }
    }
}
#endif
