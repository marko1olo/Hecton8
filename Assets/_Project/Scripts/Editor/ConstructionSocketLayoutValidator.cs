#if UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using Hecton8.Construction;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEditor.Build;

namespace Hecton8.Editor
{
    [InitializeOnLoad]
    internal static class ConstructionSocketLayoutValidator
    {
        static ConstructionSocketLayoutValidator()
        {
            ValidateOrThrow();
        }

        [MenuItem("Hecton8/Construction/Validate Socket DTO Layout")]
        public static void ValidateOrThrow()
        {
            if (!ShinobuSocketConstructionRuntime.ValidateStructLayout())
                throw new BuildFailedException(BuildLayoutReport());
        }

        private static string BuildLayoutReport()
        {
            return "SHINOBU_217 Socket DTO layout failure. " +
                   "SocketStateDTO size=" + UnsafeUtility.SizeOf<SocketStateDTO>() +
                   " LocalOffset@" + Offset<SocketStateDTO>(nameof(SocketStateDTO.LocalOffset)) +
                   " NormalDirection@" + Offset<SocketStateDTO>(nameof(SocketStateDTO.NormalDirection)) +
                   " AllowedConnectionBitmask@" + Offset<SocketStateDTO>(nameof(SocketStateDTO.AllowedConnectionBitmask)) +
                   " ParentModuleHash@" + Offset<SocketStateDTO>(nameof(SocketStateDTO.ParentModuleHash)) +
                   " ConnectionStatus@" + Offset<SocketStateDTO>(nameof(SocketStateDTO.ConnectionStatus));
        }

        private static int Offset<T>(string fieldName) where T : struct
        {
            try
            {
                return Marshal.OffsetOf<T>(fieldName).ToInt32();
            }
            catch (Exception)
            {
                return -1;
            }
        }
    }
}
#endif
