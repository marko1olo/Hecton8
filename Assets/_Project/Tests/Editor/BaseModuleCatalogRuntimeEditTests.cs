using System.IO;
using Hecton.Localization;
using Hecton8.Building;
using Hecton8.Construction;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class BaseModuleCatalogRuntimeEditTests
    {
        private const string RuntimeSourcePath = "Assets/_Project/Scripts/Construction/BaseModuleCatalogRuntime.cs";

        [Test]
        public void CatalogRuntime_HasNoGeneratedMockRoute()
        {
            string source = File.ReadAllText(RuntimeSourcePath);
            AssertNoToken(source, "Schedule", "MockCatalog");
            AssertNoToken(source, "Generate", "MockModuleCatalogJob");
            AssertNoToken(source, "CatalogGenerated", "MockFlag");
            AssertNoToken(source, "Mock", "CorridorHash");
            AssertNoToken(source, "ModuleCatalogHydrationStatus.", "Mock");
        }

        [Test]
        public void CatalogDTOs_AreEightByteAlignedAndExactSize()
        {
            Assert.That(UnsafeUtility.SizeOf<ModuleDefinitionDTO>(), Is.EqualTo(BaseModuleCatalogRuntime.ModuleDefinitionSize));
            Assert.That(UnsafeUtility.SizeOf<SocketDefinitionDTO>(), Is.EqualTo(BaseModuleCatalogRuntime.SocketDefinitionSize));
            Assert.That(UnsafeUtility.SizeOf<ModuleCostDTO>(), Is.EqualTo(BaseModuleCatalogRuntime.ModuleCostSize));
            Assert.That(UnsafeUtility.SizeOf<ModuleCatalogStateDTO>(), Is.EqualTo(BaseModuleCatalogRuntime.StateSize));
            Assert.That(UnsafeUtility.SizeOf<ModuleCatalogTelemetryEntry>(), Is.EqualTo(BaseModuleCatalogRuntime.TelemetryEntrySize));
            Assert.That(BaseModuleCatalogRuntime.ValidateLayout(
                out int moduleSize,
                out int socketSize,
                out int costSize,
                out int stateSize,
                out int telemetrySize), Is.True);
            Assert.That((moduleSize | socketSize | costSize | stateSize | telemetrySize) & 7, Is.EqualTo(0));
        }

        [Test]
        public void TemplateRoute_BuildsModuleAndSocketDTOs()
        {
            BaseModuleTemplate template = ScriptableObject.CreateInstance<BaseModuleTemplate>();
            try
            {
                const string stableId = "H8_TEST_CATALOG_MODULE";
                const string compatibility = "pressure-hull";
                int templateHash = LocHash.Compute(stableId);
                SerializedObject serialized = new SerializedObject(template);
                serialized.FindProperty("stableId").stringValue = stableId;
                serialized.FindProperty("templateHashId").intValue = templateHash;
                serialized.FindProperty("proxyBoundsSize").vector3Value = new Vector3(6f, 4f, 10f);

                SerializedProperty sockets = serialized.FindProperty("socketDefinitions");
                sockets.arraySize = 1;
                SerializedProperty socket = sockets.GetArrayElementAtIndex(0);
                socket.FindPropertyRelative("localPosition").vector3Value = new Vector3(3f, 0f, 0f);
                socket.FindPropertyRelative("direction").enumValueIndex = (int)ModuleSocketDirection.East;
                socket.FindPropertyRelative("compatibleType").stringValue = compatibility;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(BaseModuleCatalogRuntime.TryBuildModuleFromTemplate(template, 7, out ModuleDefinitionDTO module), Is.True);
                Assert.That(module.PrefabHashID, Is.EqualTo(unchecked((uint)templateHash)));
                Assert.That(module.SocketCount, Is.EqualTo(1u));
                Assert.That(module.SocketStartIndex, Is.EqualTo(7));
                Assert.That(module.BoundingBoxExtents, Is.EqualTo(new float3(3f, 2f, 5f)));

                Assert.That(BaseModuleCatalogRuntime.TryBuildSocketFromTemplate(template, 0, out SocketDefinitionDTO socketDto), Is.True);
                Assert.That(socketDto.LocalOffset, Is.EqualTo(new float3(3f, 0f, 0f)));
                Assert.That(socketDto.Normal, Is.EqualTo(new float3(1f, 0f, 0f)));
                Assert.That(socketDto.AllowedConnectionsMask, Is.EqualTo(BaseModuleCatalogRuntime.ComputeCompatibilityMask(compatibility)));
            }
            finally
            {
                Object.DestroyImmediate(template);
            }
        }

        private static void AssertNoToken(string source, string prefix, string suffix)
        {
            Assert.That(source.Contains(string.Concat(prefix, suffix)), Is.False);
        }
    }
}
