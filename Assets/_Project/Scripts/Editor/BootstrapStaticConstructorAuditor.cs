using System;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using Hecton8.Core;

namespace Hecton8.EditorValidation
{
    [InitializeOnLoad]
    internal static class BootstrapStaticConstructorAuditor
    {
        private const string MenuPath = "Hecton8/Validation/Bootstrap Static Constructor Audit";

        static BootstrapStaticConstructorAuditor()
        {
            EditorApplication.delayCall -= Validate;
            EditorApplication.delayCall += Validate;
        }

        [MenuItem(MenuPath)]
        private static void ValidateMenu()
        {
            Validate();
        }

        private static void Validate()
        {
            StringBuilder failures = null;
            global::System.Reflection.Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int assemblyIndex = 0; assemblyIndex < assemblies.Length; assemblyIndex++)
            {
                Type[] types;
                try
                {
                    types = assemblies[assemblyIndex].GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    types = exception.Types;
                }

                if (types == null)
                    continue;

                for (int typeIndex = 0; typeIndex < types.Length; typeIndex++)
                {
                    Type type = types[typeIndex];
                    if (type == null ||
                        type.TypeInitializer == null ||
                        HasBeforeFieldInit(type) ||
                        !ImplementsSystemContract(type))
                    {
                        continue;
                    }

                    failures ??= new StringBuilder(512);
                    failures.Append(type.FullName).Append('\n');
                }
            }

            if (failures == null)
                return;

            string message =
                "[BootstrapStaticConstructorAuditor] Static constructor found on ISystem type. " +
                "Static constructors are forbidden for deterministic boot:\n" +
                failures;
            H8Debug.LogError(message);
            throw new InvalidOperationException(message);
        }

        private static bool ImplementsSystemContract(Type type)
        {
            Type[] interfaces = type.GetInterfaces();
            for (int i = 0; i < interfaces.Length; i++)
            {
                Type candidate = interfaces[i];
                if (candidate != null && string.Equals(candidate.Name, "ISystem", StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool HasBeforeFieldInit(Type type)
        {
            return (type.Attributes & TypeAttributes.BeforeFieldInit) != 0;
        }
    }
}
