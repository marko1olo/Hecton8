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
        private static readonly MethodInfo SignalSizeOfMethod =
            typeof(SignalPayloadLayoutValidator).GetMethod(
                nameof(SizeOfSignalGeneric),
                BindingFlags.Static | BindingFlags.NonPublic);

        private static readonly MethodInfo UnmanagedSizeOfMethod =
            typeof(SignalPayloadLayoutValidator).GetMethod(
                nameof(SizeOfUnmanagedGeneric),
                BindingFlags.Static | BindingFlags.NonPublic);

        private static readonly string[] ExplicitNativeQueuePayloadTypeNames =
        {
            "Hecton8.Inventory.InventoryEventPayload",
            "Hecton8.Inventory.InventoryPhysicalDropRequestPayload",
            "Hecton8.Gameplay.MeteorShowerEvent",
            "Hecton8.Gameplay.RandomEventStartedPayload",
            "Hecton8.Gameplay.SeismicShockwaveEvent",
            "Hecton8.UI.BaseIntegrityEventPayload",
            "Hecton8.Modding.long3",
            "Hecton8.Modding.ModAup",
            "Hecton8.Modding.ModAupCommand",
            "Hecton8.Modding.ModAupResponse",
            "Hecton8.Modding.ModCommand",
            "Hecton8.Modding.ModCriticalMemoryEvictionPayload",
            "Hecton8.Modding.ModEventDto",
            "Hecton8.Modding.ModRaycastResultPayload",
            "Hecton8.Modding.ModRegistryEventPayload",
            "Hecton8.Modding.ModRenderInstanceCommand"
        };

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
                        type.IsGenericTypeDefinition)
                    {
                        continue;
                    }

                    if (typeof(ISignal).IsAssignableFrom(type))
                        ValidateSignalType(type, ref report);
                    else if (IsExplicitNativeQueuePayload(type))
                        ValidateNativeQueuePayloadType(type, ref report);
                }
            }

            if (report != null)
                throw new InvalidOperationException(report.ToString());
        }

        private static void ValidateSignalType(Type type, ref StringBuilder report)
        {
            ValidateExplicitRuntimePayload(
                type,
                SignalSizeOfMethod,
                "Pack parameter is forbidden for ARM64 signal payloads.",
                "Signal payloads must declare [StructLayout(LayoutKind.Explicit, Size = N)].",
                true,
                ref report);
        }

        private static void ValidateNativeQueuePayloadType(Type type, ref StringBuilder report)
        {
            ValidateExplicitRuntimePayload(
                type,
                UnmanagedSizeOfMethod,
                "Pack parameter is forbidden for ARM64 native queue payloads.",
                "Signal-adjacent native queue payloads must declare [StructLayout(LayoutKind.Explicit, Size = N)].",
                false,
                ref report);
        }

        private static void ValidateExplicitRuntimePayload(
            Type type,
            MethodInfo sizeOfMethod,
            string packMessage,
            string layoutMessage,
            bool requireSignalQueueStride,
            ref StringBuilder report)
        {
            StructLayoutAttribute layout = type.StructLayoutAttribute;
            if (HasExplicitStructLayoutPack(type))
                Append(ref report, type, packMessage);

            if (layout == null || layout.Value != LayoutKind.Explicit)
                Append(ref report, type, layoutMessage);

            int size = ResolveUnsafeSize(type, sizeOfMethod, ref report);
            if (size > 0 && (size & 7) != 0)
                Append(ref report, type, "UnsafeUtility.SizeOf<T>() must be a multiple of 8 bytes.");

            if (requireSignalQueueStride && size > 0 && !IsAllowedSignalPayloadSize(size))
                Append(ref report, type, "Signal payload size must be positive, 8-byte aligned, and at most 192 bytes.");
        }

        private static bool HasExplicitStructLayoutPack(Type type)
        {
            foreach (CustomAttributeData attribute in CustomAttributeData.GetCustomAttributes(type))
            {
                if (attribute.AttributeType != typeof(StructLayoutAttribute))
                    continue;

                foreach (CustomAttributeNamedArgument argument in attribute.NamedArguments)
                {
                    if (string.Equals(argument.MemberName, nameof(StructLayoutAttribute.Pack), StringComparison.Ordinal))
                        return true;
                }
            }

            return false;
        }

        private static bool IsAllowedSignalPayloadSize(int size)
        {
            return size > 0 && size <= 192 && (size & 7) == 0;
        }

        private static bool IsExplicitNativeQueuePayload(Type type)
        {
            string fullName = type.FullName;
            if (string.IsNullOrEmpty(fullName))
                return false;

            for (int i = 0; i < ExplicitNativeQueuePayloadTypeNames.Length; i++)
            {
                if (string.Equals(fullName, ExplicitNativeQueuePayloadTypeNames[i], StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static int ResolveUnsafeSize(Type type, MethodInfo sizeOfMethod, ref StringBuilder report)
        {
            try
            {
                MethodInfo generic = sizeOfMethod.MakeGenericMethod(type);
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

        private static int SizeOfSignalGeneric<T>()
            where T : unmanaged, ISignal
        {
            return UnsafeUtility.SizeOf<T>();
        }

        private static int SizeOfUnmanagedGeneric<T>()
            where T : unmanaged
        {
            return UnsafeUtility.SizeOf<T>();
        }
    }
}
#endif
