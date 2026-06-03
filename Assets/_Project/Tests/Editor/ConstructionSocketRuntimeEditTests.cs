using System.IO;
using Hecton8.Construction;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;

namespace Hecton8.Tests.Editor
{
    public sealed class ConstructionSocketRuntimeEditTests
    {
        private const string SocketDataPath = "Assets/_Project/Scripts/Construction/ShinobuSocketConstructionData.cs";
        private const string SocketJobsPath = "Assets/_Project/Scripts/Construction/ShinobuSocketConstructionJobs.cs";
        private const string SocketEditorPath = "Assets/_Project/Scripts/Editor/ConstructionSocketEditorTools.cs";

        [Test]
        public void SocketConstruction_HasNoSyntheticGridPublicationRoute()
        {
            AssertNoToken(File.ReadAllText(SocketDataPath), "Generate", "MockBaseConstructionGrid");
            AssertNoToken(File.ReadAllText(SocketDataPath), "s_", "Mock");
            AssertNoToken(File.ReadAllText(SocketJobsPath), "Generate", "MockBuilderGhostValidationJob");
            AssertNoToken(File.ReadAllText(SocketEditorPath), "Generate 500 Module ", "Mock Grid");
        }

        [Test]
        public void SocketConstruction_CapacityAndLayoutAreAuthoritative()
        {
            Assert.That(ShinobuSocketConstructionRuntime.ModuleCapacity, Is.EqualTo(500));
            Assert.That(ShinobuSocketConstructionRuntime.SocketCapacity, Is.EqualTo(
                ShinobuSocketConstructionRuntime.ModuleCapacity * ShinobuSocketConstructionRuntime.SocketsPerModuleCapacity));
            Assert.That(FoundationSnappingCalculatorRuntime.ModuleCapacity, Is.EqualTo(ShinobuSocketConstructionRuntime.ModuleCapacity));
            Assert.That(UnsafeUtility.SizeOf<ConstructionSocketModuleDTO>(), Is.EqualTo(ShinobuSocketConstructionRuntime.ConstructionSocketModuleSizeBytes));
            Assert.That(UnsafeUtility.SizeOf<SocketStateDTO>(), Is.EqualTo(ShinobuSocketConstructionRuntime.SocketStateSizeBytes));
            Assert.That(UnsafeUtility.SizeOf<BuilderGhostStateDTO>(), Is.EqualTo(ShinobuSocketConstructionRuntime.BuilderGhostStateSizeBytes));
            Assert.That(ShinobuSocketConstructionRuntime.ValidateStructLayout(), Is.True);
        }

        private static void AssertNoToken(string source, string prefix, string suffix)
        {
            Assert.That(source.Contains(string.Concat(prefix, suffix)), Is.False);
        }
    }
}
