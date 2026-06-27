using UnityEngine;
using System.IO;
using Unity.Collections.LowLevel.Unsafe;
using Hecton8.Celestial;
using Unity.Mathematics;

namespace Hecton8.Graphics
{
    /// <summary>
    /// Cold-loads binary visual tuning data and applies it to specific visual systems.
    /// Strictly zero-GC in hot paths.
    /// </summary>
    public class HectonVisualsOrchestrator : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private HectonCelestialEngine _celestialEngine;
        [SerializeField] private Material _oceanMaterial;

        private const string BinaryPath = "Hecton8/DataMonolith/visual_tuning.h8bin";

        private void Awake()
        {
            LoadAndApplyVisuals();
        }

        public void LoadAndApplyVisuals()
        {
            string fullPath = Path.Combine(Application.streamingAssetsPath, BinaryPath);
            if (!File.Exists(fullPath))
            {
                Debug.LogWarning($"[HectonVisualsOrchestrator] Missing tuning binary at {fullPath}. Using defaults.");
                ApplyState(VisualTuningState.Default());
                return;
            }

            try
            {
                // File I/O allowed ONLY during cold initialization/staged reload, NOT in Tick.
                byte[] data = File.ReadAllBytes(fullPath);
                
                int expectedSize = UnsafeUtility.SizeOf<VisualTuningState>();
                if (data.Length != expectedSize)
                {
                    Debug.LogError($"[HectonVisualsOrchestrator] Size mismatch. Expected {expectedSize}, got {data.Length}.");
                    return;
                }

                VisualTuningState state = default;
                unsafe
                {
                    fixed (byte* ptr = data)
                    {
                        UnsafeUtility.CopyPtrToStructure(ptr, out state);
                    }
                }

                ApplyState(state);
                Debug.Log("[HectonVisualsOrchestrator] Successfully applied binary visual tuning state.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[HectonVisualsOrchestrator] Failed to load binary visual tuning: {e.Message}");
            }
        }

        private void ApplyState(VisualTuningState state)
        {
            if (_oceanMaterial != null)
            {
                _oceanMaterial.SetColor("_ScatterColourBase", new Color(state.OceanScatterBase.x, state.OceanScatterBase.y, state.OceanScatterBase.z, state.OceanScatterBase.w));
                _oceanMaterial.SetColor("_ScatterColourShallow", new Color(state.OceanScatterShallow.x, state.OceanScatterShallow.y, state.OceanScatterShallow.z, state.OceanScatterShallow.w));
                _oceanMaterial.SetFloat("_ScatterColourShallowDepthMax", state.OceanScatterShallowDepthMax);
            }

            if (_celestialEngine != null)
            {
                _celestialEngine.ApplyTuningState(state.PlanetCenterRadius, state.SunIntensity, new Color(state.SunColor.x, state.SunColor.y, state.SunColor.z, state.SunColor.w));
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap()
        {
            if (FindAnyObjectByType<HectonVisualsOrchestrator>() == null)
            {
                var go = new GameObject("HectonVisualsOrchestrator_Auto");
                var orch = go.AddComponent<HectonVisualsOrchestrator>();
                
                orch._celestialEngine = FindAnyObjectByType<HectonCelestialEngine>();
                
                var oceanRenderer = FindAnyObjectByType<Renderer>(); // Crest.OceanRenderer might not be available in this namespace easily, fallback to finding by name or tag if needed
                // Instead, just load the shared material from AssetDatabase in Editor, or we can just rely on the celestial engine for now if ocean material is missing.
                // Wait, Crest OceanRenderer is in Crest namespace.
#if UNITY_EDITOR
                orch._oceanMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Crest/Crest/Materials/Ocean.mat");
#endif
            }
        }
    }
}
