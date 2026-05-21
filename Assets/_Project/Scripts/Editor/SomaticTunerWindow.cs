#if UNITY_EDITOR
using System;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.EditorTools
{
    public sealed class SomaticTunerWindow : EditorWindow
    {
        private const float VectorScale = 0.35f;
        private const string ComfortCsvPath = "Data/UX/vr_comfort_profiles.csv";

        // COLD ALLOC: Vector3[300] - editor-only comfort telemetry graph scratch - owner: SomaticTunerWindow
        private static readonly Vector3[] s_graphPoints = new Vector3[300];
        private IMGUIContainer _uiToolkitComfortPanel;

        [MenuItem("HECTON-8/Somatic Tuner")]
        private static void Open()
        {
            GetWindow<SomaticTunerWindow>("Somatic Tuner");
        }

        [MenuItem("HECTON-8/Somatic Comfort Tuner")]
        private static void OpenComfort()
        {
            GetWindow<SomaticTunerWindow>("Somatic Comfort Tuner");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui -= OnSceneGui;
            SceneView.duringSceneGui += OnSceneGui;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGui;
        }

        private void CreateGUI()
        {
            rootVisualElement.Clear();
            _uiToolkitComfortPanel = new IMGUIContainer(() => DrawComfortTuner(GlobalRegistry.DataVault));
            rootVisualElement.Add(_uiToolkitComfortPanel);
            rootVisualElement.schedule.Execute(() =>
            {
                if (_uiToolkitComfortPanel != null)
                    _uiToolkitComfortPanel.MarkDirtyRepaint();
            }).Every(250);
        }

        private void OnGUI()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null ||
                !TryReadFirst(vault, BufferID.ShinobuSomaticTuning, out SomaticKinematicsTuningData tuning))
            {
                EditorGUILayout.LabelField("No SHINOBU kinematic tuning buffer.");
                DrawComfortTuner(vault);
                return;
            }

            EditorGUI.BeginChangeCheck();
            tuning.BaseDrag = EditorGUILayout.Slider("Base Drag", tuning.BaseDrag, 0.01f, 8.0f);
            tuning.StrokeMultiplier = EditorGUILayout.Slider("Stroke Multiplier", tuning.StrokeMultiplier, 0.1f, 30.0f);
            tuning.SeaglideAcceleration = EditorGUILayout.Slider("Seaglide Acceleration", tuning.SeaglideAcceleration, 0.1f, 40.0f);
            tuning.SurfaceBuoyancy = EditorGUILayout.Slider("Surface Buoyancy", tuning.SurfaceBuoyancy, 0.1f, 40.0f);
            if (EditorGUI.EndChangeCheck())
                TryWriteFirst(vault, BufferID.ShinobuSomaticTuning, tuning);

            if (TryReadExistingVaultView(vault, BufferID.ShinobuSomaticBlackBoxCursor, out NativeArray<int> cursor) &&
                TryReadExistingVaultView(vault, BufferID.ShinobuSomaticBlackBox, out NativeArray<SomaticKinematicBlackBoxEntry> ring) &&
                cursor.Length > 0 &&
                ring.Length > 0)
            {
                int index = PositiveModulo(cursor[0] - 1, ring.Length);
                SomaticKinematicBlackBoxEntry entry = ring[index];
                EditorGUILayout.LabelField("Frame", entry.Frame.ToString());
                EditorGUILayout.LabelField("Velocity", ToVector3(entry.Velocity).ToString("F3"));
                EditorGUILayout.LabelField("Thrust", ToVector3(entry.RequestedThrust).ToString("F3"));
                EditorGUILayout.LabelField("Push-Out", ToVector3(entry.SdfPushOut).ToString("F3"));
            }

            DrawComfortTuner(vault);
        }

        private void OnSceneGui(SceneView sceneView)
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null ||
                !TryReadExistingVaultView(vault, BufferID.ShinobuSomaticBlackBoxCursor, out NativeArray<int> cursor) ||
                !TryReadExistingVaultView(vault, BufferID.ShinobuSomaticBlackBox, out NativeArray<SomaticKinematicBlackBoxEntry> ring) ||
                cursor.Length == 0 ||
                ring.Length == 0)
            {
                return;
            }

            SomaticKinematicBlackBoxEntry entry = ring[PositiveModulo(cursor[0] - 1, ring.Length)];
            Vector3 origin = ToVector3(entry.LocalPosition);
            Handles.color = Color.blue;
            Handles.DrawLine(origin, origin + (ToVector3(entry.RequestedThrust) * VectorScale), 2.0f);
            Handles.color = Color.red;
            Handles.DrawLine(origin, origin + (ToVector3(entry.SdfPushOut) * 2.0f), 2.0f);
            Handles.color = Color.green;
            Handles.DrawLine(origin, origin + (ToVector3(entry.Velocity) * VectorScale), 2.0f);
        }

        private static void DrawComfortTuner(IDataVault vault)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("VR Somatic Comfort", EditorStyles.boldLabel);
            if (vault == null)
            {
                EditorGUILayout.LabelField("No DataVault.");
                return;
            }

            if (TryReadFirst(vault, BufferID.ShinobuVRSomaticProfile, out VrComfortProfileDTO profile))
            {
                EditorGUI.BeginChangeCheck();
                profile.UserComfortWeight01 = EditorGUILayout.Slider("Comfort Weight", profile.UserComfortWeight01, 0f, 1f);
                profile.FovAggressiveness = EditorGUILayout.Slider("Tunneling Aggressiveness", profile.FovAggressiveness, 0f, 2f);
                profile.HorizonLockSpeed = EditorGUILayout.Slider("Horizon Lock Speed", profile.HorizonLockSpeed, 0f, 32f);
                profile.FoveatedBaseline = EditorGUILayout.Slider("Foveated Baseline", profile.FoveatedBaseline, 0f, 0.5f);
                profile.EwmaSharpness = EditorGUILayout.Slider("EWMA Sharpness", profile.EwmaSharpness, 0.1f, 40f);
                if (EditorGUI.EndChangeCheck())
                    TryWriteFirst(vault, BufferID.ShinobuVRSomaticProfile, profile);

                if (GUILayout.Button("Import vr_comfort_profiles.csv"))
                    ImportComfortCsv(vault);
            }
            else
            {
                EditorGUILayout.LabelField("No VR comfort profile buffer.");
            }

            if (TryReadFirst(vault, BufferID.ShinobuVRSomaticComfortRead, out SomaticComfortStateDTO state))
            {
                EditorGUILayout.FloatField("FOV Tunnel", state.FovTunnelingIntensity);
                EditorGUILayout.FloatField("Horizon Lock", state.HorizonLockBlend);
                EditorGUILayout.FloatField("Foveated Scale", state.FoveatedScaleMultiplier);
            }

            if (TryReadExistingVaultView(vault, BufferID.ShinobuVRSomaticComfortTelemetry, out NativeArray<ComfortTelemetryEntry> telemetry) &&
                telemetry.Length > 1)
            {
                Rect rect = GUILayoutUtility.GetRect(320f, 96f, GUILayout.ExpandWidth(true));
                DrawComfortGraph(rect, telemetry);
            }
        }

        private static unsafe void ImportComfortCsv(IDataVault vault)
        {
            VaultGenerationHandle<byte> scratchHandle = default;
            VaultGenerationHandle<VrComfortProfileDTO> profilesHandle = default;
            VaultGenerationHandle<VrComfortProfileLookupSlotDTO> lookupHandle = default;
            bool scratchLocked = false;
            bool profilesLocked = false;
            bool lookupLocked = false;
            if (vault == null ||
                !File.Exists(ComfortCsvPath) ||
                !TryAcquireEditorWriteView(vault, BufferID.ShinobuVRSomaticCsvScratch, out scratchHandle, out NativeArray<byte> scratch))
            {
                return;
            }

            scratchLocked = true;
            try
            {
                int byteCount = ReadFileIntoScratch(ComfortCsvPath, scratch);
                if (byteCount <= 0)
                    return;

                if (!TryAcquireEditorWriteView(vault, BufferID.ShinobuVRSomaticProfile, out profilesHandle, out NativeArray<VrComfortProfileDTO> profiles) ||
                    profiles.Length == 0)
                {
                    return;
                }

                profilesLocked = true;
                void* source = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(scratch);
                ReadOnlySpan<byte> csv = new ReadOnlySpan<byte>(source, byteCount);
                if (TryAcquireEditorWriteView(vault, BufferID.ShinobuVRSomaticProfileLookup, out lookupHandle, out NativeArray<VrComfortProfileLookupSlotDTO> lookup))
                {
                    lookupLocked = true;
                    VRSomaticProvider.ParseComfortProfilesCsv(csv, profiles, lookup);
                    return;
                }

                VRSomaticProvider.ParseComfortProfilesCsv(csv, profiles);
            }
            finally
            {
                if (lookupLocked)
                    vault.ReleaseWriteLock(in lookupHandle, SystemID.CoreDiagnostics);
                if (profilesLocked)
                    vault.ReleaseWriteLock(in profilesHandle, SystemID.CoreDiagnostics);
                if (scratchLocked)
                    vault.ReleaseWriteLock(in scratchHandle, SystemID.CoreDiagnostics);
            }
        }

        private static unsafe int ReadFileIntoScratch(string path, NativeArray<byte> scratch)
        {
            try
            {
                using (FileStream stream = File.OpenRead(path))
                {
                    if (stream.Length <= 0L || stream.Length > scratch.Length || stream.Length > int.MaxValue)
                        return -1;

                    int length = (int)stream.Length;
                    void* destination = NativeArrayUnsafeUtility.GetUnsafePtr(scratch);
                    Span<byte> target = new Span<byte>(destination, length);
                    int totalRead = 0;
                    while (totalRead < length)
                    {
                        int read = stream.Read(target.Slice(totalRead));
                        if (read <= 0)
                            return -1;

                        totalRead += read;
                    }

                    return totalRead == length ? totalRead : -1;
                }
            }
            catch (IOException)
            {
                return -1;
            }
            catch (UnauthorizedAccessException)
            {
                return -1;
            }
        }

        private static void DrawComfortGraph(Rect rect, NativeArray<ComfortTelemetryEntry> telemetry)
        {
            EditorGUI.DrawRect(rect, new Color(0.06f, 0.07f, 0.08f, 1f));
            int count = Mathf.Min(s_graphPoints.Length, telemetry.Length);
            if (count < 2)
                return;

            BuildGraphPoints(rect, telemetry, count, true);
            Handles.color = new Color(0.25f, 0.65f, 1f, 1f);
            Handles.DrawAAPolyLine(2f, count, s_graphPoints);
            BuildGraphPoints(rect, telemetry, count, false);
            Handles.color = new Color(1f, 0.35f, 0.22f, 1f);
            Handles.DrawAAPolyLine(2f, count, s_graphPoints);
        }

        private static void BuildGraphPoints(Rect rect, NativeArray<ComfortTelemetryEntry> telemetry, int count, bool angularVelocity)
        {
            float step = rect.width / Mathf.Max(1, count - 1);
            for (int i = 0; i < count; i++)
            {
                ComfortTelemetryEntry entry = telemetry[i];
                float value = angularVelocity
                    ? Mathf.Clamp01(entry.PeakAngularVelocityRadS / 16f)
                    : Mathf.Clamp01(entry.FovTunnelingIntensity);
                s_graphPoints[i] = new Vector3(rect.x + (step * i), rect.yMax - (value * rect.height), 0f);
            }
        }

        private static bool TryReadFirst<T>(IDataVault vault, BufferID bufferId, out T value)
            where T : struct
        {
            value = default;
            if (!TryReadExistingVaultView(vault, bufferId, out NativeArray<T> buffer) || buffer.Length == 0)
                return false;

            value = buffer[0];
            return true;
        }

        private static bool TryWriteFirst<T>(IDataVault vault, BufferID bufferId, in T value)
            where T : struct
        {
            if (!TryAcquireEditorWriteView(vault, bufferId, out VaultGenerationHandle<T> handle, out NativeArray<T> buffer))
                return false;

            try
            {
                if (buffer.Length == 0)
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

        private static bool TryAcquireEditorWriteView<T>(
            IDataVault vault,
            BufferID bufferId,
            out VaultGenerationHandle<T> handle,
            out NativeArray<T> buffer)
            where T : struct
        {
            handle = default;
            buffer = default;
            return vault != null &&
                   vault.TryGetGenerationHandle(bufferId, out handle) &&
                   vault.TryAcquireWriteLock(in handle, SystemID.CoreDiagnostics, out buffer) &&
                   buffer.IsCreated;
        }

        private static int PositiveModulo(int value, int length)
        {
            int modulo = value % length;
            return modulo < 0 ? modulo + length : modulo;
        }

        private static Vector3 ToVector3(Unity.Mathematics.float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }
    }
}
#endif
