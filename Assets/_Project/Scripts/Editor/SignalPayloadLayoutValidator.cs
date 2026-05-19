#if UNITY_EDITOR
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Hecton8.Core.Contracts.Signals;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;

namespace Hecton8.Editor
{
    [InitializeOnLoad]
    internal static class SignalPayloadLayoutValidator
    {
        private static readonly MethodInfo SizeOfMethod =
            typeof(SignalPayloadLayoutValidator).GetMethod(
                nameof(SizeOfGeneric),
                BindingFlags.Static | BindingFlags.NonPublic);

        static SignalPayloadLayoutValidator()
        {
            ValidateAllSignalLayouts();
        }

        [MenuItem("Hecton8/Diagnostics/Validate Signal Payload Layouts")]
        public static void ValidateAllSignalLayouts()
        {
            StringBuilder report = null;
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int assemblyIndex = 0; assemblyIndex < assemblies.Length; assemblyIndex++)
            {
                Type[] types;
                try
                {
                    types = assemblies[assemblyIndex].GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types;
                }

                if (types == null)
                    continue;

                for (int typeIndex = 0; typeIndex < types.Length; typeIndex++)
                {
                    Type type = types[typeIndex];
                    if (type == null ||
                        !type.IsValueType ||
                        type.IsGenericTypeDefinition ||
                        !typeof(ISignal).IsAssignableFrom(type))
                    {
                        continue;
                    }

                    ValidateType(type, ref report);
                }
            }

            if (report != null)
                throw new InvalidOperationException(report.ToString());
        }

        private static void ValidateType(Type type, ref StringBuilder report)
        {
            StructLayoutAttribute layout = type.StructLayoutAttribute;
            if (layout != null && layout.Pack == 1)
                Append(ref report, type, "Pack=1 is forbidden for ARM64 signal payloads.");

            if (layout == null || layout.Value != LayoutKind.Explicit)
                Append(ref report, type, "Signal payloads must declare [StructLayout(LayoutKind.Explicit, Size = N)].");

            int size = ResolveUnsafeSize(type, ref report);
            if (size > 0 && (size & 7) != 0)
                Append(ref report, type, "UnsafeUtility.SizeOf<T>() must be a multiple of 8 bytes.");
        }

        private static int ResolveUnsafeSize(Type type, ref StringBuilder report)
        {
            try
            {
                MethodInfo generic = SizeOfMethod.MakeGenericMethod(type);
                return (int)generic.Invoke(null, null);
            }
            catch (Exception ex)
            {
                Append(ref report, type, "UnsafeUtility.SizeOf<T>() failed: " + ex.GetType().Name);
                return -1;
            }
        }

        private static void Append(ref StringBuilder report, Type type, string message)
        {
            if (report == null)
                report = new StringBuilder(512);

            report.Append(type.FullName);
            report.Append(": ");
            report.AppendLine(message);
        }

        private static int SizeOfGeneric<T>()
            where T : unmanaged, ISignal
        {
            return UnsafeUtility.SizeOf<T>();
        }
    }
}
#endif
