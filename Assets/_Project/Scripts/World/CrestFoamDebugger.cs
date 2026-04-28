using Crest;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Play-mode forensic probe for Crest foam settings and sampled water height.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-10000)]
    public sealed class CrestFoamDebugger : MonoBehaviour
    {
        [Header("Forensics")]
        [SerializeField]
        [Tooltip("If enabled, force Crest foam fade to a high value on Awake for immediate foam dissipation during forensic runs.")]
        private bool forceFoamFadeRate = true;

        [SerializeField, UnityEngine.RangeAttribute(0f, 20f)]
        [Tooltip("Foam fade-rate override applied during forensic runs.")]
        private float forcedFoamFadeRate = 20f;

        private readonly SampleHeightHelper _sampleHeightHelper = new SampleHeightHelper(); // COLD ALLOC: SampleHeightHelper[1] — one-shot Crest water-height forensic probe — owner: CrestFoamDebugger

        private void Awake()
        {
            OceanRenderer ocean = OceanRenderer.Instance;
            if (ocean == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[CrestFoamDebugger] OceanRenderer.Instance is null.");
#endif
                return;
            }

            SimSettingsFoam foamSettings = ocean._simSettingsFoam;
            float sampledWaveHeight = ocean.SeaLevel;
            bool sampledWaveHeightSuccessfully = false;
            if (ocean.Viewpoint != null)
            {
                _sampleHeightHelper.Init(ocean.Viewpoint.position, 2f, false, this);
                sampledWaveHeightSuccessfully = _sampleHeightHelper.Sample(out sampledWaveHeight);
            }

            if (forceFoamFadeRate && foamSettings != null)
                foamSettings._foamFadeRate = forcedFoamFadeRate;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(
                $"[CrestFoamDebugger] FoamFadeRate={(foamSettings != null ? foamSettings._foamFadeRate : -1f):0.###} " +
                $"SimulateFoam={ocean.CreateFoamSim} " +
                $"WaveHeight={(sampledWaveHeightSuccessfully ? sampledWaveHeight : ocean.SeaLevel):0.###} " +
                $"SeaLevel={ocean.SeaLevel:0.###} " +
                $"ViewpointSampleSucceeded={sampledWaveHeightSuccessfully}");
#endif
        }
    }
}
