using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class RuntimeOriginAupFallbackGuardEditTests
    {
        [Test]
        public void RuntimeOriginFallbacks_RejectNonFiniteOriginBeforeAbsoluteConversion()
        {
            string droneSource = Read("_Project/Scripts/Construction/DroneFleetManager.cs");
            string tetherSource = Read("_Project/Scripts/TetherManager.cs");
            string thermodynamicsSource = Read("_Project/Scripts/Thermodynamics/AbyssalThermodynamicsSolver.cs");
            string droneReference = ExtractMethodBody(droneSource, "private static double3 ResolveDroneRenderReferenceAup()");
            string droneOrigin = ExtractMethodBody(droneSource, "private static double3 ResolveRuntimeOriginAupDouble3()");
            string tetherCamera = ExtractMethodBody(tetherSource, "private bool ResolveShinobu132CameraContext(out Vector3 cameraPosition, out double3 cameraAup)");
            string tetherOrigin = ExtractMethodBody(tetherSource, "private static bool TryResolveRuntimeOriginAup(out double3 absoluteAup)");
            string buildTuning = ExtractMethodBody(thermodynamicsSource, "private ThermalGridTuningDTO BuildTuning()");
            string tryResolveAnchor = ExtractMethodBody(thermodynamicsSource, "private bool TryResolveAnchorAup(out double3 anchorAup)");
            string thermodynamicsOrigin = ExtractMethodBody(thermodynamicsSource, "private static double3 ResolveRuntimeOriginAupDouble3()");

            StringAssert.Contains("return ResolveRuntimeOriginAupDouble3();", droneReference);
            StringAssert.Contains("originAup.IsFinite() ? originAup.ToAbsoluteDouble3() : double3.zero", droneOrigin);
            StringAssert.DoesNotContain("CurrentRuntimeOriginAup().ToAbsoluteDouble3()", droneReference);
            StringAssert.Contains("if (!TryResolveRuntimeOriginAup(out cameraAup))", tetherCamera);
            StringAssert.Contains("cameraAup = double3.zero;", tetherCamera);
            StringAssert.Contains("if (!originAup.IsFinite())", tetherOrigin);
            StringAssert.Contains("return math.all(math.isfinite(absoluteAup));", tetherOrigin);
            StringAssert.DoesNotContain("CurrentRuntimeOriginAup().ToAbsoluteDouble3()", tetherCamera);
            StringAssert.Contains(": ResolveRuntimeOriginAupDouble3();", buildTuning);
            StringAssert.Contains("if (!originAup.IsFinite())", tryResolveAnchor);
            StringAssert.Contains("originAup.IsFinite() ? originAup.ToAbsoluteDouble3() : double3.zero", thermodynamicsOrigin);
            StringAssert.DoesNotContain("CurrentRuntimeOriginAup().ToAbsoluteDouble3()", buildTuning);
        }

        private static string Read(string projectRelativePath)
        {
            return File.ReadAllText(Path.Combine(Application.dataPath, projectRelativePath));
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            int start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0, "Missing method: " + signature);
            int brace = source.IndexOf('{', start);
            Assert.GreaterOrEqual(brace, 0, "Missing method body: " + signature);

            int depth = 0;
            for (int i = brace; i < source.Length; i++)
            {
                if (source[i] == '{')
                    depth++;
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(brace, i - brace + 1);
                }
            }

            Assert.Fail("Unclosed method body: " + signature);
            return string.Empty;
        }
    }
}
