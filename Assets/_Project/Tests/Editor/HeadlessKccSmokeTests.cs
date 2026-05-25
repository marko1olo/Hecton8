#if UNITY_EDITOR
using System.IO;
using Hecton8.Physics.KCC;
using Hecton8.Physics.KCC.Editor;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class HeadlessKccSmokeTests
    {
        [Test]
        public void OceanKinematicsRuntimeService_HasNoForbiddenHeadlessDependency()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string path = Path.Combine(projectRoot, "Assets", "_Project", "Scripts", "Core", "OceanKinematicsRuntimeService.cs");
            string source = File.ReadAllText(path);

            Assert.IsFalse(source.Contains("Camera.main"), "OceanKinematicsRuntimeService must not read Camera.main.");
            Assert.IsFalse(source.Contains("Time.deltaTime"), "OceanKinematicsRuntimeService must use injected tick dt.");
            Assert.IsFalse(source.Contains("FindObjectOfType"), "OceanKinematicsRuntimeService must not search the scene.");
            Assert.IsFalse(source.Contains("GameObject.Find"), "OceanKinematicsRuntimeService must not search the scene.");
        }

        [Test]
        public void HeadlessKcc_Layouts_AreExplicitAndAligned()
        {
            HeadlessKccLayoutAssertions.AssertAll();
        }

        [Test]
        public void HeadlessKcc_SmokeRunner_UsesShinobu355SingleHeavyEntryPoint()
        {
            Assert.AreEqual(100, HydrodynamicKccRuntime.KccSmokeDefaultPhantomCount);
            Assert.AreEqual(10000, HydrodynamicKccRuntime.KccSmokeDefaultFrameCount);
        }

        [Test]
        public void HeadlessKcc_SmokeRunner_Preserves100MpsConeProbe()
        {
            bool valid = Shinobu355KccSmokeRunner.ValidateApexConeFallContract(out float displacementPerFrameMeters, out float tuningMaxSpeedMetersPerSecond);
            Assert.IsTrue(valid);
            Assert.AreEqual(1.6666667f, displacementPerFrameMeters, 0.0001f);
            Assert.GreaterOrEqual(tuningMaxSpeedMetersPerSecond, Shinobu355KccSmokeRunner.ConeFallProofSpeedMetersPerSecond);
        }
    }
}
#endif
