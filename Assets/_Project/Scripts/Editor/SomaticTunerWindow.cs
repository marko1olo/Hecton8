#if UNITY_EDITOR
using System;
using System.Globalization;
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
        private const int ComfortCsvScratchBytes = 4096;
        private const int ComfortProfileHashScratchCapacity = 4;
        private const int ComfortProfileLookupScratchCapacity = 8;

        // COLD ALLOC: Vector3[300] - editor-only comfort telemetry graph scratch - owner: SomaticTunerWindow
        private static readonly Vector3[] s_graphPoints = new Vector3[300];
        // COLD ALLOC: byte[4096] - editor-only VR comfort CSV import scratch - owner: SomaticTunerWindow
        private static readonly byte[] s_comfortCsvImportScratch = new byte[ComfortCsvScratchBytes];
        // COLD ALLOC: uint[4] - editor-only VR comfort profile hash staging - owner: SomaticTunerWindow
        private static readonly uint[] s_comfortProfileHashScratch = new uint[ComfortProfileHashScratchCapacity];
        // COLD ALLOC: VrComfortProfileLookupSlotDTO[8] - editor-only lookup staging - owner: SomaticTunerWindow
        private static readonly VrComfortProfileLookupSlotDTO[] s_comfortProfileLookupScratch = new VrComfortProfileLookupSlotDTO[ComfortProfileLookupScratchCapacity];
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
                EditorGUILayout.LabelField("Velocity", FormatVector3(ToVector3(entry.Velocity)));
                EditorGUILayout.LabelField("Thrust", FormatVector3(ToVector3(entry.RequestedThrust)));
                EditorGUILayout.LabelField("Push-Out", FormatVector3(ToVector3(entry.SdfPushOut)));
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

        private static void ImportComfortCsv(IDataVault vault)
        {
            VaultGenerationHandle<VrComfortProfileDTO> profilesHandle = default;
            VaultGenerationHandle<VrComfortProfileLookupSlotDTO> lookupHandle = default;
            bool profilesLocked = false;
            bool lookupLocked = false;
            if (vault == null ||
                !File.Exists(ComfortCsvPath))
            {
                return;
            }

            int byteCount = ReadFileIntoScratch(ComfortCsvPath, s_comfortCsvImportScratch);
            if (byteCount <= 0)
                return;

            ReadOnlySpan<byte> csv = new ReadOnlySpan<byte>(s_comfortCsvImportScratch, 0, byteCount);
            int profileCount = 0;
            try
            {
                if (!TryAcquireEditorWriteView(vault, BufferID.ShinobuVRSomaticProfile, out profilesHandle, out NativeArray<VrComfortProfileDTO> profiles) ||
                    profiles.Length == 0)
                {
                    return;
                }

                profilesLocked = true;
                profileCount = VRSomaticProvider.ParseComfortProfilesCsv(csv, profiles, s_comfortProfileHashScratch);
            }
            finally
            {
                if (profilesLocked)
                    vault.ReleaseWriteLock(in profilesHandle, SystemID.CoreDiagnostics);
            }

            if (profileCount <= 0)
                return;

            BuildComfortProfileLookupScratch(s_comfortProfileHashScratch, profileCount, s_comfortProfileLookupScratch);
            try
            {
                if (TryAcquireEditorWriteView(vault, BufferID.ShinobuVRSomaticProfileLookup, out lookupHandle, out NativeArray<VrComfortProfileLookupSlotDTO> lookup))
                {
                    lookupLocked = true;
                    CopyComfortProfileLookupScratchToVault(lookup, s_comfortProfileLookupScratch);
                }
            }
            finally
            {
                if (lookupLocked)
                    vault.ReleaseWriteLock(in lookupHandle, SystemID.CoreDiagnostics);
            }
        }

        private static void BuildComfortProfileLookupScratch(
            uint[] profileHashes,
            int profileCount,
            VrComfortProfileLookupSlotDTO[] lookup)
        {
            Array.Clear(lookup, 0, lookup.Length);

            int count = Math.Min(Math.Max(0, profileCount), profileHashes.Length);
            for (int i = 0; i < count; i++)
                InsertComfortProfileLookup(lookup, profileHashes[i], i);
        }

        private static void InsertComfortProfileLookup(
            VrComfortProfileLookupSlotDTO[] lookup,
            uint profileHash,
            int profileIndex)
        {
            if (lookup == null || lookup.Length == 0 || profileHash == 0u)
                return;

            int start = (int)(profileHash % (uint)lookup.Length);
            for (int i = 0; i < lookup.Length; i++)
            {
                int slotIndex = (start + i) % lookup.Length;
                VrComfortProfileLookupSlotDTO slot = lookup[slotIndex];
                if (slot.Occupied == 0u || slot.ProfileHash == profileHash)
                {
                    lookup[slotIndex] = new VrComfortProfileLookupSlotDTO
                    {
                        ProfileHash = profileHash,
                        ProfileIndex = profileIndex,
                        Occupied = 1u
                    };
                    return;
                }
            }
        }

        private static unsafe void CopyComfortProfileLookupScratchToVault(
            NativeArray<VrComfortProfileLookupSlotDTO> lookup,
            VrComfortProfileLookupSlotDTO[] source)
        {
            if (!lookup.IsCreated || lookup.Length <= 0 || source == null)
                return;

            int copyCount = Math.Min(lookup.Length, source.Length);
            void* targetPtr = NativeArrayUnsafeUtility.GetUnsafePtr(lookup);
            UnsafeUtility.MemClear(targetPtr, (long)lookup.Length * UnsafeUtility.SizeOf<VrComfortProfileLookupSlotDTO>());
            if (copyCount <= 0)
                return;

            fixed (VrComfortProfileLookupSlotDTO* sourcePtr = source)
            {
                UnsafeUtility.MemCpy(targetPtr, sourcePtr, (long)copyCount * UnsafeUtility.SizeOf<VrComfortProfileLookupSlotDTO>());
            }
        }

        private static int ReadFileIntoScratch(string path, byte[] scratch)
        {
            try
            {
                using (FileStream stream = File.OpenRead(path))
                {
                    if (stream.Length <= 0L || stream.Length > scratch.Length || stream.Length > int.MaxValue)
                        return -1;

                    int length = (int)stream.Length;
                    Span<byte> target = scratch.AsSpan(0, length);
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
            if (vault == null ||
                !vault.TryGetGenerationHandle(bufferId, out handle) ||
                !vault.TryAcquireWriteLock(in handle, SystemID.CoreDiagnostics, out buffer))
            {
                return false;
            }

            if (buffer.IsCreated)
                return true;

            vault.ReleaseWriteLock(in handle, SystemID.CoreDiagnostics);
            buffer = default;
            return false;
        }

        private static int PositiveModulo(int value, int length)
        {
            int modulo = value % length;
            return modulo < 0 ? modulo + length : modulo;
        }

        private static string FormatVector3(Vector3 value)
        {
            return "(" +
                   value.x.ToString("F3", CultureInfo.InvariantCulture) +
                   ", " +
                   value.y.ToString("F3", CultureInfo.InvariantCulture) +
                   ", " +
                   value.z.ToString("F3", CultureInfo.InvariantCulture) +
                   ")";
        }

        private static Vector3 ToVector3(Unity.Mathematics.float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }
    }
}
#endif
