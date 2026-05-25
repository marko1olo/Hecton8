#if UNITY_EDITOR
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.World.SeedShipAnomaly.Editor
{
    public sealed class SeedShipAnomalyTunerWindow : EditorWindow
    {
        private static readonly Color s_outerColor = new Color(1f, 0.05f, 0.02f, 0.75f);
        private static readonly Color s_innerColor = new Color(1f, 0.85f, 0.05f, 0.9f);

        [MenuItem("Hecton8/Seed Ship Anomaly Tuner")]
        public static void Open()
        {
            GetWindow<SeedShipAnomalyTunerWindow>("Seed Ship Anomaly Tuner");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += DrawSceneGizmos;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawSceneGizmos;
        }

        private void OnGUI()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to bind the GlobalDataVault anomaly buffers.", MessageType.Info);
                return;
            }

            if (!TryRead(out AnomalyFieldDTO field, out AnomalyTuningDTO tuning, out AnomalyGlobalScalarsDTO globals))
            {
                EditorGUILayout.HelpBox("Seed Ship anomaly buffers are not allocated.", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("Corruption", globals.Corruption01.ToString("0.000"));
            EditorGUILayout.LabelField("Gravity Y", globals.GravityY.ToString("0.000"));
            EditorGUILayout.LabelField("Entity Budget", globals.EntityBudget.ToString());
            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();
            tuning.MaxCorruptionRadius = EditorGUILayout.Slider("Max Corruption Radius", tuning.MaxCorruptionRadius, 1f, 12000f);
            tuning.GravityInversionStrength = EditorGUILayout.Slider("Gravity Inversion Strength", tuning.GravityInversionStrength, 0f, 1f);
            tuning.PulseFrequency = EditorGUILayout.Slider("Pulse Frequency", tuning.PulseFrequency, 0.01f, 32f);
            tuning.GlitchIntensity = EditorGUILayout.Slider("Glitch Intensity", tuning.GlitchIntensity, 0f, 1f);
            tuning.HeatEmission = EditorGUILayout.Slider("Heat Emission", tuning.HeatEmission, 0f, 1f);
            tuning.RadarJamIntensity = EditorGUILayout.Slider("Radar Jam Intensity", tuning.RadarJamIntensity, 0f, 1f);
            tuning.GlobalQualityWeight = EditorGUILayout.Slider("GlobalQualityWeight Fallback", tuning.GlobalQualityWeight, 0f, 1f);
            field.Radius = tuning.MaxCorruptionRadius;
            if (EditorGUI.EndChangeCheck())
                Write(field, tuning);

            EditorGUILayout.Space();
            if (GUILayout.Button("Inject Core Hack"))
            {
                SignalBus<CoreHackedSignal>.TryPush(new CoreHackedSignal
                {
                    Frame = unchecked((uint)Mathf.Max(0, Time.frameCount)),
                    SourceHash = SeedShipAnomalyConstants.SourceHash,
                    CodeHash = SeedShipAnomalyConstants.CoreHackAcceptedHash,
                    Validity01 = 1f,
                    Flags = 1
                });
            }

            Repaint();
            SceneView.RepaintAll();
        }

        private static bool TryRead(out AnomalyFieldDTO field, out AnomalyTuningDTO tuning, out AnomalyGlobalScalarsDTO globals)
        {
            field = default;
            tuning = default;
            globals = default;
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
                return false;

            if (!TryReadExistingView(vault, BufferID.ShinobuSeedShipAnomalyField, out NativeArray<AnomalyFieldDTO>.ReadOnly fieldArray) ||
                !TryReadExistingView(vault, BufferID.ShinobuSeedShipAnomalyTuning, out NativeArray<AnomalyTuningDTO>.ReadOnly tuningArray) ||
                !TryReadExistingView(vault, BufferID.ShinobuSeedShipAnomalyGlobals, out NativeArray<AnomalyGlobalScalarsDTO>.ReadOnly globalsArray))
            {
                return false;
            }

            if (!fieldArray.IsCreated || fieldArray.Length == 0 ||
                !tuningArray.IsCreated || tuningArray.Length == 0 ||
                !globalsArray.IsCreated || globalsArray.Length == 0)
            {
                return false;
            }

            field = fieldArray[0];
            tuning = tuningArray[0];
            globals = globalsArray[0];
            return true;
        }

        private static void Write(AnomalyFieldDTO field, AnomalyTuningDTO tuning)
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
                return;

            if (!vault.TryGetGenerationHandle(BufferID.ShinobuSeedShipAnomalyField, out VaultGenerationHandle<AnomalyFieldDTO> fieldHandle) ||
                !vault.TryGetGenerationHandle(BufferID.ShinobuSeedShipAnomalyTuning, out VaultGenerationHandle<AnomalyTuningDTO> tuningHandle))
            {
                return;
            }

            if (!vault.TryLockBuffer(BufferID.ShinobuSeedShipAnomalyField, SystemID.EndgameAnomaly))
                return;

            bool tuningLocked = false;
            try
            {
                tuningLocked = vault.TryLockBuffer(BufferID.ShinobuSeedShipAnomalyTuning, SystemID.EndgameAnomaly);
                if (TryOpenExistingView(vault, in fieldHandle, out NativeArray<AnomalyFieldDTO> fieldArray))
                {
                    field.Radius = math.max(0f, field.Radius);
                    field.CorruptionLevel = math.saturate(field.CorruptionLevel);
                    fieldArray[0] = field;
                }

                if (tuningLocked &&
                    TryOpenExistingView(vault, in tuningHandle, out NativeArray<AnomalyTuningDTO> tuningArray))
                {
                    tuningArray[0] = SeedShipAnomalyMath.SanitizeTuning(tuning);
                }
            }
            finally
            {
                if (tuningLocked)
                    vault.TryUnlockBuffer(BufferID.ShinobuSeedShipAnomalyTuning, SystemID.EndgameAnomaly);
                vault.TryUnlockBuffer(BufferID.ShinobuSeedShipAnomalyField, SystemID.EndgameAnomaly);
            }
        }

        private static void DrawSceneGizmos(SceneView view)
        {
            if (!Application.isPlaying || !TryRead(out AnomalyFieldDTO field, out _, out AnomalyGlobalScalarsDTO globals))
                return;

            double3 aup = field.EpicenterAUP;
            Vector3 center = new Vector3((float)aup.x, (float)aup.y, (float)aup.z);
            float pulse = 1f + 0.035f * MathLodApproximation.ApproxSinBhaskara((float)EditorApplication.timeSinceStartup * 3.5f);
            float outerRadius = Mathf.Max(1f, field.Radius) * pulse;
            float innerRadius = Mathf.Max(1f, field.Radius * Mathf.Lerp(0.45f, 0.65f, Mathf.Clamp01(globals.Corruption01)));

            Handles.color = s_outerColor;
            DrawWireSphere(center, outerRadius);
            Handles.color = s_innerColor;
            DrawWireSphere(center, innerRadius);
        }

        private static void DrawWireSphere(Vector3 center, float radius)
        {
            Handles.DrawWireDisc(center, Vector3.up, radius);
            Handles.DrawWireDisc(center, Vector3.right, radius);
            Handles.DrawWireDisc(center, Vector3.forward, radius);
        }

        private static bool TryReadExistingView<T>(
            IDataVault vault,
            BufferID bufferId,
            out NativeArray<T>.ReadOnly buffer) where T : struct
        {
            buffer = default;
            return vault != null &&
                   vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> handle) &&
                   vault.TryReadOnlyHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length > 0;
        }

        private static bool TryOpenExistingView<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            return vault != null &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length > 0;
        }
    }
}
#endif
