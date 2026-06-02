using System.IO;
using Hecton8.Editor.Bakers;
using NUnit.Framework;

namespace Hecton8.Tests.Editor.Bakers
{
    public sealed class ApexIntegratorVerifier1605EditTests
    {
        private const string BakerRoot = "Assets/_Project/Editor/Bakers";
        private const string VerifierPath = "Assets/_Project/Editor/Bakers/ApexIntegratorVerifier1605.cs";

        [Test]
        public void ApexVerifier_PassesOnBakerSources()
        {
            bool passed = ApexIntegratorVerifier1605.RunSourceVerification(out ApexVerificationResult1605 result);

            Assert.IsTrue(passed, result.FirstViolation);
            Assert.GreaterOrEqual(result.SourceFileCount, 4);
            Assert.GreaterOrEqual(result.HotMethodCount, 1);
            Assert.AreEqual(0, result.DataVaultTokenCount);
            Assert.AreEqual(0, result.ViolationCount);
        }

        [Test]
        public void ApexVerifier_SourceContainsNoReportOrBuildSpawnerPath()
        {
            string verifier = File.ReadAllText(VerifierPath);

            Assert.That(verifier, Does.Contain("RunSourceVerification"));
            Assert.That(verifier, Does.Contain("TryCollectSourceFiles"));
            Assert.That(verifier, Does.Contain("TryReadSourceFile"));
            Assert.That(verifier, Does.Contain("source file enumeration failed"));
            Assert.That(verifier, Does.Contain("source file read failed"));
            Assert.That(verifier, Does.Contain("s_requiredMemoryCeilingTokens"));
            Assert.That(verifier, Does.Contain("VerifyRequiredMemoryCeilingTokens"));
            Assert.That(verifier, Does.Contain("missing required memory ceiling token"));
            Assert.That(verifier, Does.Contain("s_requiredTransactionSafetyTokens"));
            Assert.That(verifier, Does.Contain("VerifyRequiredTransactionSafetyTokens"));
            Assert.That(verifier, Does.Contain("missing required transaction safety token"));
            Assert.That(verifier, Does.Contain("TryResolveComputeKernel"));
            Assert.That(verifier, Does.Contain("TryFindPackedRectForSource"));
            Assert.That(verifier, Does.Contain("IsRecoverableEditorException"));
            Assert.That(verifier, Does.Contain("TryRestoreTextureReadableState"));
            Assert.That(verifier, Does.Contain("TryCaptureAssetFileRollbackSnapshots(albedoPath, normalPath, maskPath, materialPath"));
            Assert.That(verifier, Does.Contain("VerifierSourceFileName"));
            Assert.That(verifier, Does.Contain("file.EndsWith(VerifierSourceFileName, StringComparison.Ordinal)"));
            Assert.That(verifier, Does.Contain("MaxAtlasSourcePixels"));
            Assert.That(verifier, Does.Contain("Array.Sort(files, StringComparer.Ordinal)"));
            Assert.That(verifier, Does.Contain("StripCommentsAndStrings"));
            Assert.That(verifier, Does.Contain("VerifyDataVaultLocks"));
            Assert.That(verifier, Does.Not.Contain("WriteAllText"));
            Assert.That(verifier, Does.Not.Contain("WriteAllBytes"));
            Assert.That(verifier, Does.Not.Contain("using System.Diagnostics"));
            Assert.That(verifier, Does.Not.Contain("new ProcessStartInfo"));
            Assert.That(verifier, Does.Not.Contain("File.Create"));
        }

        [Test]
        public void BakerDomain_HasNoRuntimePhaseOrVaultAuthoritySurface()
        {
            string[] files = Directory.GetFiles(BakerRoot, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string normalized = files[i].Replace('\\', '/');
                if (normalized.EndsWith("/ApexIntegratorVerifier1605.cs", System.StringComparison.Ordinal))
                    continue;

                string source = File.ReadAllText(files[i]);

                Assert.That(source, Does.Not.Contain("GlobalRegistry.Get<"), files[i]);
                Assert.That(source, Does.Not.Contain("GlobalDataVault"), files[i]);
                Assert.That(source, Does.Not.Contain("TryGetLatestCreated("), files[i]);
                Assert.That(source, Does.Not.Contain("void Update("), files[i]);
                Assert.That(source, Does.Not.Contain("void FixedUpdate("), files[i]);
                Assert.That(source, Does.Not.Contain("void LateUpdate("), files[i]);
                Assert.That(source, Does.Not.Contain("LateFrameTick"), files[i]);
                Assert.That(source, Does.Not.Contain("SystemDispatcher."), files[i]);
            }
        }
    }
}
