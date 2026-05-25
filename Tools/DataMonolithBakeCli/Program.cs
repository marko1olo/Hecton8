using System;
using System.IO;
using Hecton8.EditorValidation;

namespace Hecton8.Tools.DataMonolithBakeCli
{
    internal static class Program
    {
        public static int Main(string[] args)
        {
            string projectRoot = args.Length > 0 ? Path.GetFullPath(args[0]) : FindProjectRoot();
            if (string.IsNullOrEmpty(projectRoot) || !Directory.Exists(projectRoot))
            {
                Console.Error.WriteLine("Project root not found.");
                return 2;
            }

            Directory.SetCurrentDirectory(projectRoot);
            UnityEngine.Application.dataPath = Path.Combine(projectRoot, "Assets");
            UnityEngine.Application.version = ReadUnityBundleVersion(projectRoot);

            bool baked = H8DataMonolithCompiler.BakeAll(logSummary: true);
            string error = string.Empty;
            bool valid = baked && H8DataMonolithCompiler.TryValidateOutputBlob(out error);
            if (!valid)
            {
                Console.Error.WriteLine("Data Monolith bake failed: " + error + " last=" + H8DataMonolithCompiler.LastError);
                return 1;
            }

            bool fuzzed = H8DataMonolithCorruptionFuzzer.Run();
            if (!fuzzed)
            {
                Console.Error.WriteLine("Data Monolith corruption fuzzer failed.");
                return 3;
            }

            bool loadStressPassed = DataMonolithLoadStressProbe.Run(projectRoot);
            if (!loadStressPassed)
            {
                Console.Error.WriteLine("Data Monolith load stress probe failed.");
                return 4;
            }

            bool failClosedPassed = DataMonolithFailClosedProbe.Run(projectRoot);
            if (!failClosedPassed)
            {
                Console.Error.WriteLine("Data Monolith fail-closed runtime simulation failed.");
                return 5;
            }

            bool playerParserAbsencePassed = DataMonolithPlayerParserAbsenceProbe.Run(projectRoot);
            if (!playerParserAbsencePassed)
            {
                Console.Error.WriteLine("Data Monolith player parser absence probe failed.");
                return 6;
            }

            Console.WriteLine("Data Monolith baked: " + H8DataMonolithCompiler.OutputAssetPath);
            return 0;
        }

        private static string FindProjectRoot()
        {
            string current = Directory.GetCurrentDirectory();
            while (!string.IsNullOrEmpty(current))
            {
                if (File.Exists(Path.Combine(current, "ProjectSettings", "ProjectSettings.asset")) &&
                    Directory.Exists(Path.Combine(current, "Assets")))
                {
                    return current;
                }

                current = Directory.GetParent(current)?.FullName;
            }

            return string.Empty;
        }

        private static string ReadUnityBundleVersion(string projectRoot)
        {
            string path = Path.Combine(projectRoot, "ProjectSettings", "ProjectSettings.asset");
            if (!File.Exists(path))
                return "0.0.0";

            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                const string prefix = "bundleVersion:";
                if (line.StartsWith(prefix, StringComparison.Ordinal))
                    return line.Substring(prefix.Length).Trim();
            }

            return "0.0.0";
        }
    }
}
