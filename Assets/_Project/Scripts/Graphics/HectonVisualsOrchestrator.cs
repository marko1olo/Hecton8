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
            if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
            {
                Debug.LogWarning($"[HectonVisualsOrchestrator] Missing tuning binary at {fullPath}. Using defaults.");
                ApplyState(VisualTuningState.Default());
                return;
            }

            try
            {
                VisualTuningState state = LoadValidatedVisualTuningState(fullPath);
                ApplyState(state);
                Debug.Log("[HectonVisualsOrchestrator] Successfully applied binary visual tuning state.");
            }
            catch (DataCorruptionException exception)
            {
                Debug.LogError($"[HectonVisualsOrchestrator] Visual tuning binary rejected: {exception.Message}");
                throw;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[HectonVisualsOrchestrator] Failed to load binary visual tuning: {e.Message}");
                throw;
            }
        }

        private static unsafe VisualTuningState LoadValidatedVisualTuningState(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
                throw new DataCorruptionException($"Missing tuning binary at {fullPath}.");

            int expectedSize = UnsafeUtility.SizeOf<VisualTuningState>();
            FileInfo info = new FileInfo(fullPath);
            if (info.Length < expectedSize)
                throw new DataCorruptionException($"Visual tuning payload too small. Expected at least {expectedSize} bytes, got {info.Length}.");

            if (info.Length != expectedSize)
                throw new DataCorruptionException($"Visual tuning payload size mismatch. Expected exactly {expectedSize} bytes, got {info.Length}.");

            VisualTuningState state = default;
            using (FileStream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, expectedSize, FileOptions.SequentialScan))
            {
                byte* destination = (byte*)UnsafeUtility.AddressOf(ref state);
                int totalRead = 0;
                while (totalRead < expectedSize)
                {
                    int read = stream.Read(new System.Span<byte>(destination + totalRead, expectedSize - totalRead));
                    if (read <= 0)
                        throw new DataCorruptionException($"Visual tuning payload ended after {totalRead} of {expectedSize} bytes.");

                    totalRead += read;
                }
            }

            ValidateFinite(in state);
            return state;
        }

        private static void ValidateFinite(in VisualTuningState state)
        {
            if (!math.all(math.isfinite(state.OceanScatterBase)) ||
                !math.all(math.isfinite(state.OceanScatterShallow)) ||
                !math.isfinite(state.PlanetCenterRadius) ||
                !math.all(math.isfinite(state.SunColor)) ||
                !math.isfinite(state.OceanScatterShallowDepthMax) ||
                !math.isfinite(state.SunIntensity) ||
                !math.isfinite(state.Exposure))
            {
                throw new DataCorruptionException("Visual tuning payload contains non-finite values.");
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

        private sealed class DataCorruptionException : System.Exception
        {
            public DataCorruptionException(string message)
                : base(message)
            {
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
