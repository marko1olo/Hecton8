#if UNITY_EDITOR
using System.Reflection;
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

        [MenuItem("HECTON-8/Terminal OS/Validate DTO Layouts")]
        public static void ValidateLayoutsMenu()
        {
            ValidateLayouts(true);
        }

        public static bool ValidateLayouts(bool logSuccess)
        {
            bool ok = true;
            ok &= ValidateSize<TerminalInteractionDTO>(32);
            ok &= ValidateOffset<TerminalInteractionDTO>(nameof(TerminalInteractionDTO.TerminalHash), 0);
            ok &= ValidateOffset<TerminalInteractionDTO>(nameof(TerminalInteractionDTO.LocalHitUV), 4);
            ok &= ValidateOffset<TerminalInteractionDTO>(nameof(TerminalInteractionDTO.InteractionFlags), 12);
            ok &= ValidateOffset<TerminalInteractionDTO>(nameof(TerminalInteractionDTO.Distance), 16);
            ok &= ValidateOffset<TerminalInteractionDTO>(nameof(TerminalInteractionDTO._pad0), 20);
            ok &= ValidateOffset<TerminalInteractionDTO>(nameof(TerminalInteractionDTO._pad11), 31);
            ok &= ValidateSize<ButtonAABBDTO>(32);
            ok &= ValidateSize<GazeRayDTO>(80);
            ok &= ValidateSize<TerminalPlaneDTO>(128);
            ok &= ValidateSize<TerminalTelemetryEntry>(64);

            if (ok && logSuccess)
                Debug.Log("[SHINOBU_137] Terminal OS DTO layout validated.");

            return ok;
        }

        private static bool ValidateSize<T>(int expected) where T : struct
        {
            int observed = UnsafeUtility.SizeOf<T>();
            if (observed == expected)
                return true;

            Debug.LogError("[SHINOBU_137] DTO size mismatch: " + typeof(T).Name + " expected " + expected + " observed " + observed);
            return false;
        }

        private static bool ValidateOffset<T>(string fieldName, int expected) where T : struct
        {
            FieldInfo field = typeof(T).GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            int observed = field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
            if (observed == expected)
                return true;

            Debug.LogError("[SHINOBU_137] DTO offset mismatch: " + typeof(T).Name + "." + fieldName + " expected " + expected + " observed " + observed);
            return false;
        }
    }
}
#endif
