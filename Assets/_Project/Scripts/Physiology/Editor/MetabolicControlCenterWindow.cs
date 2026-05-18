#if UNITY_EDITOR
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Physiology.Editor
{
    /// <summary>
    /// Editor-only bridge for designers. Runtime remains unmanaged; this window only reads and writes the vault row.
    /// </summary>
    public sealed class MetabolicControlCenterWindow : EditorWindow
    {
        private const float HistogramHeight = 96f;
        private const float BarGap = 2f;

        private readonly float[] _tissueScratch = new float[ShinobuPhysiologyConstants.TissueCompartmentCount]; // EDITOR ONLY: histogram staging
        private readonly float[] _mValueScratch = new float[ShinobuPhysiologyConstants.TissueCompartmentCount]; // EDITOR ONLY: histogram staging
        private int _entityIndex;

        [MenuItem("Hecton8/Physiology/Metabolic Control Center")]
        public static void Open()
        {
            GetWindow<MetabolicControlCenterWindow>("Metabolic Control");
        }

        private void OnGUI()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to edit vault-backed physiology tuning.", MessageType.Info);
                return;
            }

            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
            {
                EditorGUILayout.HelpBox("GlobalDataVault is not registered.", MessageType.Warning);
                return;
            }

            NativeArray<PhysiologyTuningDTO> tuningArray = vault.GetBuffer<PhysiologyTuningDTO>(
                BufferID.ShinobuPhysiologyTuning,
                1,
                SystemID.GameplayPlayer,
                NativeArrayOptions.ClearMemory);

            if (!tuningArray.IsCreated || tuningArray.Length == 0)
            {
                EditorGUILayout.HelpBox("Physiology tuning buffer is not available.", MessageType.Warning);
                return;
            }

            PhysiologyTuningDTO tuning = ShinobuPhysiologyJobMath.SanitizeTuning(tuningArray[0]);
            EditorGUI.BeginChangeCheck();
            tuning.BaseO2DrainPerSecond = EditorGUILayout.Slider("Base O2 Drain", tuning.BaseO2DrainPerSecond, 0.00001f, 0.05f);
            tuning.NitrogenUptakeRate = EditorGUILayout.Slider("Nitrogen Uptake Rate", tuning.NitrogenUptakeRate, 0.05f, 16f);
            tuning.AdrenalineDecaySeconds = EditorGUILayout.Slider("Adrenaline Decay", tuning.AdrenalineDecaySeconds, 1f, 180f);
            tuning.HypothermiaCoolingRate = EditorGUILayout.Slider("Hypothermia Cooling Rate", tuning.HypothermiaCoolingRate, 0.0001f, 0.05f);
            if (EditorGUI.EndChangeCheck())
                tuningArray[0] = ShinobuPhysiologyJobMath.SanitizeTuning(tuning);

            _entityIndex = EditorGUILayout.IntSlider("Entity Row", _entityIndex, 0, 63);
            ReadHistogram(vault, _entityIndex);
            DrawHistogram();
        }

        private void ReadHistogram(IDataVault vault, int entityIndex)
        {
            NativeArray<DecompressionStateDTO> states = vault.GetBuffer<DecompressionStateDTO>(
                BufferID.ShinobuDecompressionStates,
                math.max(1, entityIndex + 1),
                SystemID.GameplayPlayer,
                NativeArrayOptions.ClearMemory);

            NativeArray<HaldaneTissueCoefficientDTO> coefficients = vault.GetBuffer<HaldaneTissueCoefficientDTO>(
                BufferID.ShinobuHaldaneCoefficients,
                ShinobuPhysiologyConstants.TissueCompartmentCount,
                SystemID.GameplayPlayer,
                NativeArrayOptions.ClearMemory);

            if (!states.IsCreated ||
                !coefficients.IsCreated ||
                (uint)entityIndex >= (uint)states.Length ||
                coefficients.Length < ShinobuPhysiologyConstants.TissueCompartmentCount)
            {
                for (int i = 0; i < ShinobuPhysiologyConstants.TissueCompartmentCount; i++)
                {
                    _tissueScratch[i] = 0f;
                    _mValueScratch[i] = 1f;
                }

                return;
            }

            DecompressionStateDTO state = states[entityIndex];
            unsafe
            {
                float* tissues = state.TissueTensions;
                {
                    for (int i = 0; i < ShinobuPhysiologyConstants.TissueCompartmentCount; i++)
                    {
                        _tissueScratch[i] = math.max(0f, tissues[i]);
                        _mValueScratch[i] = math.max(0.1f, state.AmbientPressure * math.max(1.01f, coefficients[i].MValueRatio));
                    }
                }
            }
        }

        private void DrawHistogram()
        {
            Rect rect = GUILayoutUtility.GetRect(1f, HistogramHeight, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.08f, 0.09f, 0.1f, 1f));

            float maxValue = 1f;
            for (int i = 0; i < ShinobuPhysiologyConstants.TissueCompartmentCount; i++)
                maxValue = math.max(maxValue, math.max(_tissueScratch[i], _mValueScratch[i]));

            float barWidth = math.max(1f, (rect.width - BarGap * (ShinobuPhysiologyConstants.TissueCompartmentCount - 1)) / ShinobuPhysiologyConstants.TissueCompartmentCount);
            for (int i = 0; i < ShinobuPhysiologyConstants.TissueCompartmentCount; i++)
            {
                float normalized = math.saturate(_tissueScratch[i] / math.max(0.0001f, maxValue));
                float mValueNormalized = math.saturate(_mValueScratch[i] / math.max(0.0001f, maxValue));
                float x = rect.x + i * (barWidth + BarGap);
                float height = normalized * (rect.height - 8f);
                Rect bar = new Rect(x, rect.yMax - height - 4f, barWidth, height);
                Color color = _tissueScratch[i] > _mValueScratch[i]
                    ? new Color(0.95f, 0.18f, 0.1f, 1f)
                    : new Color(0.2f, 0.7f, 0.55f, 1f);

                EditorGUI.DrawRect(bar, color);
                float markerY = rect.yMax - mValueNormalized * (rect.height - 8f) - 4f;
                EditorGUI.DrawRect(new Rect(x, markerY, barWidth, 1f), Color.white);
            }
        }
    }
}
#endif
