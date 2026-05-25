using System;
using System.Reflection;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class HectonGeneratedProjectReferencePrunerEditTests
    {
        [Test]
        public void GeneratedProjectPruner_PreservesMissingLocalScriptAssemblyReferences()
        {
            string content =
                "<Project>" +
                "<ItemGroup>" +
                "<Reference Include=\"Hecton8.Habitat.Deformation.Contracts\">" +
                "<HintPath>Library\\ScriptAssemblies\\Hecton8.Habitat.Deformation.Contracts.dll</HintPath>" +
                "</Reference>" +
                "<Reference Include=\"Unity.Entities\">" +
                "<HintPath>Library\\PackageCache\\com.unity.entities@1.0.0\\Unity.Entities.dll</HintPath>" +
                "</Reference>" +
                "</ItemGroup>" +
                "</Project>";

            string result = InvokePruner("Hecton8.Core.csproj", content);

            Assert.That(result, Does.Contain("Hecton8.Habitat.Deformation.Contracts"));
            Assert.That(result, Does.Contain("Library\\ScriptAssemblies\\Hecton8.Habitat.Deformation.Contracts.dll"));
            Assert.That(result, Does.Not.Contain("com.unity.entities@1.0.0"));
        }

        private static string InvokePruner(string path, string content)
        {
            Type prunerType = ResolvePrunerType();
            MethodInfo method = prunerType.GetMethod(
                "OnGeneratedCSProject",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.NotNull(method, "HectonGeneratedProjectReferencePruner.OnGeneratedCSProject missing.");
            object result = method.Invoke(null, new object[] { path, content });
            return result as string;
        }

        private static Type ResolvePrunerType()
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType("Hecton8.Editor.Validation.HectonGeneratedProjectReferencePruner", false);
                if (type != null)
                    return type;
            }

            Assert.Fail("HectonGeneratedProjectReferencePruner type missing from loaded editor assemblies.");
            return null;
        }
    }
}
