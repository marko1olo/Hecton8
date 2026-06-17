#if UNITY_EDITOR
using System.Reflection;
using Hecton8.Core.Contracts;
using Hecton8.UI;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;

namespace Hecton8.UI.Editor
{
    [InitializeOnLoad]
    public static class TerminalOsLayoutValidator
    {
        static TerminalOsLayoutValidator()
        {
            ValidateLayouts(false);
        }

        [MenuItem("Hecton8/Terminal OS/Validate DTO Layouts")]
        public static void ValidateLayoutsMenu()
        {
            ValidateLayouts(true);
        }

        public static bool ValidateLayouts(bool logSuccess)
        {
            bool ok = true;
            ok &= ValidateSize<TerminalStateDTO>(48);
            ok &= ValidateOffset<TerminalStateDTO>(nameof(TerminalStateDTO.TerminalHash), 0);
            ok &= ValidateOffset<TerminalStateDTO>(nameof(TerminalStateDTO.BackgroundColor), 4);
            ok &= ValidateOffset<TerminalStateDTO>(nameof(TerminalStateDTO.IsDirty), 7);
            ok &= ValidateOffset<TerminalStateDTO>(nameof(TerminalStateDTO.Value1), 8);
            ok &= ValidateOffset<TerminalStateDTO>(nameof(TerminalStateDTO.Value2), 12);
            ok &= ValidateOffset<TerminalStateDTO>(nameof(TerminalStateDTO.TextLine), 16);
            ok &= ValidateSize<TerminalInteractionDTO>(32);
            ok &= ValidateOffset<TerminalInteractionDTO>(nameof(TerminalInteractionDTO.TerminalHash), 0);
            ok &= ValidateOffset<TerminalInteractionDTO>(nameof(TerminalInteractionDTO.LocalHitUV), 4);
            ok &= ValidateOffset<TerminalInteractionDTO>(nameof(TerminalInteractionDTO.InteractionFlags), 12);
            ok &= ValidateOffset<TerminalInteractionDTO>(nameof(TerminalInteractionDTO.Distance), 16);
            ok &= ValidateOffset<TerminalInteractionDTO>("_pad0", 20);
            ok &= ValidateOffset<TerminalInteractionDTO>("_pad11", 31);
            ok &= ValidateSize<TerminalInputStateDTO>(64);
            ok &= ValidateOffset<TerminalInputStateDTO>(nameof(TerminalInputStateDTO.TerminalAUP), 0);
            ok &= ValidateOffset<TerminalInputStateDTO>(nameof(TerminalInputStateDTO.ForwardNormal), 24);
            ok &= ValidateOffset<TerminalInputStateDTO>(nameof(TerminalInputStateDTO.UpVector), 36);
            ok &= ValidateOffset<TerminalInputStateDTO>(nameof(TerminalInputStateDTO.ProjectedUV), 48);
            ok &= ValidateOffset<TerminalInputStateDTO>(nameof(TerminalInputStateDTO.TerminalHashID), 56);
            ok &= ValidateOffset<TerminalInputStateDTO>(nameof(TerminalInputStateDTO.InputFlags), 60);
            ok &= ValidateSize<TerminalInputGpuStateDTO>(32);
            ok &= ValidateOffset<TerminalInputGpuStateDTO>(nameof(TerminalInputGpuStateDTO.ProjectedUV), 0);
            ok &= ValidateOffset<TerminalInputGpuStateDTO>(nameof(TerminalInputGpuStateDTO.TerminalHashID), 8);
            ok &= ValidateOffset<TerminalInputGpuStateDTO>(nameof(TerminalInputGpuStateDTO.InputFlags), 12);
            ok &= ValidateOffset<TerminalInputGpuStateDTO>(nameof(TerminalInputGpuStateDTO.Reserved0), 16);
            ok &= ValidateSize<TerminalInputTelemetryEntry>(64);
            ok &= ValidateSize<TerminalInputTuningDTO>(64);
            ok &= ValidateOffset<TerminalInputTuningDTO>(nameof(TerminalInputTuningDTO.MaxInteractionDistanceMeters), 0);
            ok &= ValidateOffset<TerminalInputTuningDTO>(nameof(TerminalInputTuningDTO.CursorSnappingTolerance), 4);
            ok &= ValidateOffset<TerminalInputTuningDTO>(nameof(TerminalInputTuningDTO.RaycastThickness), 8);
            ok &= ValidateOffset<TerminalInputTuningDTO>(nameof(TerminalInputTuningDTO.QualityCurvePower), 12);
            ok &= ValidateOffset<TerminalInputTuningDTO>(nameof(TerminalInputTuningDTO.LowRadiusMeters), 16);
            ok &= ValidateOffset<TerminalInputTuningDTO>(nameof(TerminalInputTuningDTO.UltraRadiusMeters), 20);
            ok &= ValidateOffset<TerminalInputTuningDTO>(nameof(TerminalInputTuningDTO.TuningFlags), 24);
            ok &= ValidateSize<ButtonAABBDTO>(32);
            ok &= ValidateSize<GazeRayDTO>(80);
            ok &= ValidateSize<TerminalPlaneDTO>(128);
            ok &= ValidateSize<TerminalTelemetryEntry>(64);
            ok &= ValidateSize<DecryptionPuzzleDTO>(32);
            ok &= ValidateOffset<DecryptionPuzzleDTO>(nameof(DecryptionPuzzleDTO.PlayerFrequency), 0);
            ok &= ValidateOffset<DecryptionPuzzleDTO>(nameof(DecryptionPuzzleDTO.PlayerPhase), 4);
            ok &= ValidateOffset<DecryptionPuzzleDTO>(nameof(DecryptionPuzzleDTO.TargetFrequency), 8);
            ok &= ValidateOffset<DecryptionPuzzleDTO>(nameof(DecryptionPuzzleDTO.TargetPhase), 12);
            ok &= ValidateOffset<DecryptionPuzzleDTO>(nameof(DecryptionPuzzleDTO.AlignmentAccuracy01), 16);
            ok &= ValidateOffset<DecryptionPuzzleDTO>(nameof(DecryptionPuzzleDTO.PuzzleID), 20);
            ok &= ValidateOffset<DecryptionPuzzleDTO>(nameof(DecryptionPuzzleDTO.Flags), 24);
            ok &= ValidateSize<DecryptionTerminalDTO>(64);
            ok &= ValidateSize<DecryptionKnobInputDTO>(64);
            ok &= ValidateSize<TerminalUnlockedSignal>(32);
            ok &= ValidateSize<DecryptionTelemetryEntry>(64);

            if (ok && logSuccess)
                Debug.Log("[SHINOBU_137/273/331] Terminal OS DTO layout validated.");

            return ok;
        }

        private static bool ValidateSize<T>(int expected) where T : unmanaged
        {
            int observed = UnsafeUtility.SizeOf<T>();
            if (observed == expected)
                return true;

            Debug.LogError("[SHINOBU_137/273/331] DTO size mismatch: " + typeof(T).Name + " expected " + expected + " observed " + observed);
            return false;
        }

        private static bool ValidateOffset<T>(string fieldName, int expected) where T : unmanaged
        {
            FieldInfo field = typeof(T).GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            int observed = field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
            if (observed == expected)
                return true;

            Debug.LogError("[SHINOBU_137/273/331] DTO offset mismatch: " + typeof(T).Name + "." + fieldName + " expected " + expected + " observed " + observed);
            return false;
        }
    }
}
#endif
