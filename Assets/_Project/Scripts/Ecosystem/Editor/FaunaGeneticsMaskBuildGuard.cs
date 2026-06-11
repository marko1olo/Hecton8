#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Hecton8.Editor
{


    public sealed class FaunaGeneticsMaskBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => 306;

        public void OnPreprocessBuild(BuildReport report)
        {
            string path = Path.Combine(Application.dataPath, "_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs");
            if (!File.Exists(path))
                return;

            string source = File.ReadAllText(path);
            if (source.IndexOf("math.asuint(aup.Local", StringComparison.Ordinal) < 0 &&
                source.IndexOf("uint low = random.NextUInt()", StringComparison.Ordinal) < 0 &&
                source.IndexOf("uint high = random.NextUInt()", StringComparison.Ordinal) < 0 &&
                source.IndexOf("PackFaunaGeneticMask", StringComparison.Ordinal) < 0 &&
                source.IndexOf("FoldFnv32(", StringComparison.Ordinal) < 0 &&
                source.IndexOf("QuantizeMetersToMillimeters(", StringComparison.Ordinal) < 0)
            {
                return;
            }

            throw new BuildFailedException("SHINOBU_306 genetic compiler drift detected. ShinobuEcosystemBalancer must route fauna genetics through Hecton8.Ecosystem.FaunaGenome64.");
        }
    }
}
#endif
