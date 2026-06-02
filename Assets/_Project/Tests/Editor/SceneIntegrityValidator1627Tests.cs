#if UNITY_EDITOR
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Editor.QA;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class SceneIntegrityValidator1627Tests
    {
        [Test]
        public void SerializedYamlMissingScriptScannerFindsFileIdZero()
        {
            const string yaml =
                "--- !u!114 &11400000\n" +
                "MonoBehaviour:\n" +
                "  m_Script: {fileID: 0}\n" +
                "  m_EditorClassIdentifier: Assembly-CSharp::Missing.Type\n";

            bool clean = SceneIntegrityValidator1627.ValidateSerializedYamlTextForMissingScripts(
                yaml,
                out int monoBehaviourCount,
                out int missingScriptCount);

            Assert.IsFalse(clean);
            Assert.AreEqual(1, monoBehaviourCount);
            Assert.AreEqual(1, missingScriptCount);
        }

        [Test]
        public void SerializedYamlMissingScriptScannerAcceptsResolvedFileId()
        {
            const string yaml =
                "--- !u!114 &11400000\n" +
                "MonoBehaviour:\n" +
                "  m_Script: {fileID: 11500000, guid: 0123456789abcdef0123456789abcdef, type: 3}\n" +
                "  m_EditorClassIdentifier: Hecton8.Core::Hecton8.Example\n";

            bool clean = SceneIntegrityValidator1627.ValidateSerializedYamlTextForMissingScripts(
                yaml,
                out int monoBehaviourCount,
                out int missingScriptCount);

            Assert.IsTrue(clean);
            Assert.AreEqual(1, monoBehaviourCount);
            Assert.AreEqual(0, missingScriptCount);
        }

        [Test]
        public void BootstrapGraphTestHookRejectsCircularOrder()
        {
            GlobalRegistryServiceSlot[] nodes =
            {
                GlobalRegistryServiceSlot.Dispatcher,
                GlobalRegistryServiceSlot.TickManager,
            };
            BootstrapRegistryDependencyEdge[] edges =
            {
                new BootstrapRegistryDependencyEdge(GlobalRegistryServiceSlot.Dispatcher, GlobalRegistryServiceSlot.TickManager),
                new BootstrapRegistryDependencyEdge(GlobalRegistryServiceSlot.TickManager, GlobalRegistryServiceSlot.Dispatcher),
            };
            GlobalRegistryServiceSlot[] order = new GlobalRegistryServiceSlot[2];

            bool ok = SceneIntegrityValidator1627.TryValidateBootstrapGraphForTest(
                nodes,
                edges,
                order,
                out int orderCount);

            Assert.IsFalse(ok);
            Assert.AreEqual(0, orderCount);
        }

        [Test]
        public void ApexHotScannerRejectsRuntimeDependencyLookup()
        {
            const string source =
                "public sealed class HotPath\n" +
                "{\n" +
                "    public void Tick(float deltaTime)\n" +
                "    {\n" +
                "        GetComponent<Camera>();\n" +
                "    }\n" +
                "}\n";

            int violations = SceneIntegrityValidator1627.CountHotDependencyLookupViolationsForTest(source);

            Assert.AreEqual(1, violations);
        }

        [Test]
        public void ApexDataVaultScannerRejectsNestedWriteLocks()
        {
            const string source =
                "public sealed class VaultHotspot\n" +
                "{\n" +
                "    public void Flush()\n" +
                "    {\n" +
                "        vault.TryAcquireWriteLock(in firstHandle, SystemID.Core, out first);\n" +
                "        vault.TryAcquireWriteLock(in secondHandle, SystemID.Core, out second);\n" +
                "    }\n" +
                "}\n";

            int violations = SceneIntegrityValidator1627.CountDataVaultWriteLockViolationsForTest(source);

            Assert.GreaterOrEqual(violations, 1);
        }

        [Test]
        public void ApexDataVaultScannerAcceptsSingleTryFinallyWriteLock()
        {
            const string source =
                "public sealed class VaultWriter\n" +
                "{\n" +
                "    public bool Commit()\n" +
                "    {\n" +
                "        if (!vault.TryAcquireWriteLock(in handle, SystemID.Core, out buffer))\n" +
                "            return false;\n" +
                "        try\n" +
                "        {\n" +
                "            return true;\n" +
                "        }\n" +
                "        finally\n" +
                "        {\n" +
                "            vault.ReleaseWriteLock(in handle, SystemID.Core);\n" +
                "        }\n" +
                "    }\n" +
                "}\n";

            int violations = SceneIntegrityValidator1627.CountDataVaultWriteLockViolationsForTest(source);

            Assert.AreEqual(0, violations);
        }

        [Test]
        public void ApexHotScannerIgnoresColdComponentLookup()
        {
            const string source =
                "public sealed class ColdOwner\n" +
                "{\n" +
                "    private void Awake()\n" +
                "    {\n" +
                "        _camera = GetComponent<Camera>();\n" +
                "    }\n" +
                "}\n";

            int violations = SceneIntegrityValidator1627.CountHotDependencyLookupViolationsForTest(source);

            Assert.AreEqual(0, violations);
        }

        [Test]
        public void ApexHotScannerRejectsVisualSyncDependencyLookup()
        {
            const string source =
                "public sealed class VisualSyncOwner\n" +
                "{\n" +
                "    public void VisualSyncTick(in DispatcherTimingDTO timing)\n" +
                "    {\n" +
                "        GlobalRegistry.Get<IUIService>();\n" +
                "    }\n" +
                "}\n";

            int violations = SceneIntegrityValidator1627.CountHotDependencyLookupViolationsForTest(source);

            Assert.AreEqual(1, violations);
        }

        [Test]
        public void ApexPresentationScannerRejectsSimulationPhaseWrite()
        {
            const string source =
                "public sealed class PresentationLeak\n" +
                "{\n" +
                "    public void Update()\n" +
                "    {\n" +
                "        Shader.SetGlobalFloat(\"_ScanPulse\", 1f);\n" +
                "    }\n" +
                "}\n";

            int violations = SceneIntegrityValidator1627.CountPresentationPhaseViolationsForTest(source);

            Assert.AreEqual(1, violations);
        }

        [Test]
        public void ApexPresentationScannerAcceptsVisualSyncWrite()
        {
            const string source =
                "public sealed class PresentationOwner\n" +
                "{\n" +
                "    public void VisualSyncTick(in DispatcherTimingDTO timing)\n" +
                "    {\n" +
                "        Shader.SetGlobalFloat(\"_ScanPulse\", timing.DeltaTime);\n" +
                "    }\n" +
                "}\n";

            int violations = SceneIntegrityValidator1627.CountPresentationPhaseViolationsForTest(source);

            Assert.AreEqual(0, violations);
        }
    }
}
#endif
