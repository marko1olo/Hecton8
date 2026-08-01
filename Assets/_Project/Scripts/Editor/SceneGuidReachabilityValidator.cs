#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Soft-FAIL CI gate for binary-aware scene/prefab script-GUID reachability.
    ///
    /// Closes the BUILD_PLAYTEST "Four scenes are BINARY" under-report gap in batchmode:
    /// text rg misses nibble-swapped GUIDs in 02_HECTON_WORLD / 010_TEST / render sandboxes.
    /// Mirrors Tools/SceneGuidReachability.py --self-test (control + Fabricator binary pin +
    /// FaunaBrain GeneratedProxy count) without spawning an external Python process.
    /// Soft FAIL stays exit 0 under -quit.
    /// </summary>
    public static class SceneGuidReachabilityValidator
    {
        private const string LogPrefix = "[SceneGuidReachabilityValidator]";

        // Known binary production / test scenes (header is NOT %YAML).
        private static readonly string[] ExpectedBinarySceneNames =
        {
            "02_HECTON_WORLD.unity",
            "010_TEST.unity",
            "020_RENDER_SANDBOX.unity",
            "020_RENDER_SANDBOX_V2.unity"
        };

        // Control type known present in live binary world (validates the search itself).
        private const string ControlTypeName = "WorldStreamingDirector";
        private const string ControlGuid = "547a39a8034a57a47b65413eb12885d2";

        // Fabricator must be reachable via nibble-swap in the production world binary scene.
        private const string FabricatorTypeName = "Fabricator";
        private const string FabricatorGuid = "65748c03d0baf8a4a95eca4dd9cfa4c4";

        // STATIC CLOSED: FaunaBrain on all 6 GeneratedProxies (not absent).
        private const string FaunaBrainTypeName = "FaunaBrain";
        private const string FaunaBrainGuid = "f97102d76d9d9d04f95ccebcd55b7079";
        private const int ExpectedFaunaBrainLiveCount = 6;

        private static readonly string[] NonLiveDirParts =
        {
            "_Recovery",
            "DEPRECATED",
            "_Archive",
            "Archive"
        };

        // COLD ALLOC: StringBuilder[4096] - editor audit report - owner: SceneGuidReachabilityValidator
        private static readonly StringBuilder Report = new StringBuilder(4096);

        /// <summary>
        /// Public for -executeMethod / CI batchmode. Soft FAIL stays exit 0 under -quit.
        /// </summary>
        [MenuItem("Hecton8/Validation/Validate Scene GUID Reachability", priority = 189)]
        public static void ValidateSceneGuidReachability()
        {
            bool batch = Application.isBatchMode;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating || Application.isPlaying)
            {
                string busy = LogPrefix + " RESULT: FAIL — Editor busy (compiling/updating/playing).";
                Debug.LogError(busy);
                if (!batch)
                    EditorUtility.DisplayDialog("Scene GUID Reachability", busy, "OK");
                return;
            }

            Report.Clear();
            Report.AppendLine("═══════════════════════════════════════════════════════");
            Report.AppendLine("HECTON-8 — Scene GUID Reachability Audit (binary-aware)");
            Report.AppendLine("═══════════════════════════════════════════════════════");
            Report.AppendLine();

            string assetsRoot = Path.GetFullPath(Application.dataPath);
            if (!Directory.Exists(assetsRoot))
            {
                FailFast(batch, "Assets root missing: " + assetsRoot);
                return;
            }

            // COLD ALLOC: lists of path/bytes pairs for editor audit only.
            List<FileBlob> textFiles = new List<FileBlob>(1024);
            List<FileBlob> binaryFiles = new List<FileBlob>(16);
            int loadFailures = 0;

            CollectSceneAndPrefabBlobs(assetsRoot, textFiles, binaryFiles, ref loadFailures);

            int total = textFiles.Count + binaryFiles.Count;
            Report.Append("files=").Append(total.ToString(CultureInfo.InvariantCulture));
            Report.Append(" text=").Append(textFiles.Count.ToString(CultureInfo.InvariantCulture));
            Report.Append(" binary=").Append(binaryFiles.Count.ToString(CultureInfo.InvariantCulture));
            Report.Append(" loadFailures=").Append(loadFailures.ToString(CultureInfo.InvariantCulture));
            Report.AppendLine();

            bool binarySetOk = true;
            for (int i = 0; i < ExpectedBinarySceneNames.Length; i++)
            {
                string name = ExpectedBinarySceneNames[i];
                bool found = false;
                for (int b = 0; b < binaryFiles.Count; b++)
                {
                    if (string.Equals(binaryFiles[b].FileName, name, StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    binarySetOk = false;
                    Report.Append("MISSING_BINARY=").Append(name).AppendLine();
                }
            }

            Report.Append("binarySetOk=").Append(binarySetOk ? 1 : 0).AppendLine();

            // Control: search must find WorldStreamingDirector or every negative is worthless.
            int controlLive;
            int controlOther;
            CountGuidHits(ControlGuid, textFiles, binaryFiles, out controlLive, out controlOther);
            bool controlOk = controlLive > 0;
            Report.Append("control=").Append(ControlTypeName);
            Report.Append(" guid=").Append(ControlGuid);
            Report.Append(" live=").Append(controlLive.ToString(CultureInfo.InvariantCulture));
            Report.Append(" other=").Append(controlOther.ToString(CultureInfo.InvariantCulture));
            Report.Append(" ok=").Append(controlOk ? 1 : 0);
            Report.AppendLine();

            // Fabricator: text form must NOT appear in world binary; nibble-swap must.
            byte[] worldBytes = null;
            string worldPath = null;
            for (int b = 0; b < binaryFiles.Count; b++)
            {
                if (string.Equals(binaryFiles[b].FileName, "02_HECTON_WORLD.unity", StringComparison.OrdinalIgnoreCase))
                {
                    worldBytes = binaryFiles[b].Data;
                    worldPath = binaryFiles[b].FullPath;
                    break;
                }
            }

            bool worldLoaded = worldBytes != null && worldBytes.Length > 0;
            bool fabricatorTextInWorld = false;
            bool fabricatorNibbleInWorld = false;
            if (worldLoaded)
            {
                byte[] fabAscii = Encoding.ASCII.GetBytes(FabricatorGuid);
                fabricatorTextInWorld = IndexOfBytes(worldBytes, fabAscii) >= 0;
                byte[] fabRaw = HexToBytes(FabricatorGuid);
                byte[] fabSwapped = NibbleSwap(fabRaw);
                fabricatorNibbleInWorld = IndexOfBytes(worldBytes, fabSwapped) >= 0 ||
                                          IndexOfBytes(worldBytes, fabRaw) >= 0;
            }

            bool fabricatorBinaryPinOk = worldLoaded && !fabricatorTextInWorld && fabricatorNibbleInWorld;
            Report.Append("fabricator=").Append(FabricatorTypeName);
            Report.Append(" guid=").Append(FabricatorGuid);
            Report.Append(" worldLoaded=").Append(worldLoaded ? 1 : 0);
            Report.Append(" textInWorld=").Append(fabricatorTextInWorld ? 1 : 0);
            Report.Append(" nibbleInWorld=").Append(fabricatorNibbleInWorld ? 1 : 0);
            Report.Append(" ok=").Append(fabricatorBinaryPinOk ? 1 : 0);
            if (!string.IsNullOrEmpty(worldPath))
                Report.Append(" world=").Append(worldPath);
            Report.AppendLine();

            // FaunaBrain: exactly 6 live GeneratedProxy prefabs.
            List<string> faunaLivePaths = new List<string>(8);
            List<string> faunaOtherPaths = new List<string>(4);
            CollectGuidHitPaths(FaunaBrainGuid, textFiles, binaryFiles, faunaLivePaths, faunaOtherPaths);
            int faunaProxyHits = 0;
            for (int i = 0; i < faunaLivePaths.Count; i++)
            {
                if (faunaLivePaths[i].IndexOf("GeneratedProxies", StringComparison.OrdinalIgnoreCase) >= 0)
                    faunaProxyHits++;
            }

            bool faunaOk = faunaLivePaths.Count == ExpectedFaunaBrainLiveCount &&
                           faunaProxyHits == ExpectedFaunaBrainLiveCount;
            Report.Append("faunaBrain=").Append(FaunaBrainTypeName);
            Report.Append(" guid=").Append(FaunaBrainGuid);
            Report.Append(" live=").Append(faunaLivePaths.Count.ToString(CultureInfo.InvariantCulture));
            Report.Append(" proxyHits=").Append(faunaProxyHits.ToString(CultureInfo.InvariantCulture));
            Report.Append(" expected=").Append(ExpectedFaunaBrainLiveCount.ToString(CultureInfo.InvariantCulture));
            Report.Append(" ok=").Append(faunaOk ? 1 : 0);
            Report.AppendLine();
            for (int i = 0; i < faunaLivePaths.Count && i < 8; i++)
                Report.Append("  faunaLive=").AppendLine(faunaLivePaths[i]);

            bool passed = loadFailures == 0 && binarySetOk && controlOk && fabricatorBinaryPinOk && faunaOk;

            Report.AppendLine();
            Report.Append(LogPrefix).Append(" RESULT: ").Append(passed ? "PASS" : "FAIL");
            Report.Append(" binarySetOk=").Append(binarySetOk ? 1 : 0);
            Report.Append(" controlOk=").Append(controlOk ? 1 : 0);
            Report.Append(" fabricatorBinaryPinOk=").Append(fabricatorBinaryPinOk ? 1 : 0);
            Report.Append(" faunaOk=").Append(faunaOk ? 1 : 0);
            Report.Append(" files=").Append(total.ToString(CultureInfo.InvariantCulture));
            Report.Append(" binary=").Append(binaryFiles.Count.ToString(CultureInfo.InvariantCulture));
            Report.AppendLine();

            string reportText = Report.ToString();
            if (passed)
                Debug.Log(reportText);
            else
                Debug.LogError(reportText);

            if (!batch)
            {
                EditorUtility.DisplayDialog(
                    "Scene GUID Reachability",
                    passed
                        ? "PASS — binary set + control + Fabricator nibble pin + FaunaBrain×6 proxies."
                        : "FAIL — see Console for measured fields.",
                    "OK");
            }

            // Soft FAIL under -quit: do not EditorApplication.Exit on audit fail.
        }

        private static void FailFast(bool batch, string message)
        {
            string line = LogPrefix + " RESULT: FAIL — " + message;
            Debug.LogError(line);
            if (!batch)
                EditorUtility.DisplayDialog("Scene GUID Reachability", line, "OK");
        }

        private static void CollectSceneAndPrefabBlobs(
            string assetsRoot,
            List<FileBlob> textFiles,
            List<FileBlob> binaryFiles,
            ref int loadFailures)
        {
            // COLD ALLOC: path arrays from Directory.GetFiles - editor audit only.
            string[] unityPaths;
            string[] prefabPaths;
            try
            {
                unityPaths = Directory.GetFiles(assetsRoot, "*.unity", SearchOption.AllDirectories);
                prefabPaths = Directory.GetFiles(assetsRoot, "*.prefab", SearchOption.AllDirectories);
            }
            catch (Exception)
            {
                loadFailures++;
                return;
            }

            AppendBlobs(unityPaths, textFiles, binaryFiles, ref loadFailures);
            AppendBlobs(prefabPaths, textFiles, binaryFiles, ref loadFailures);
        }

        private static void AppendBlobs(
            string[] paths,
            List<FileBlob> textFiles,
            List<FileBlob> binaryFiles,
            ref int loadFailures)
        {
            if (paths == null)
                return;

            for (int i = 0; i < paths.Length; i++)
            {
                string path = paths[i];
                if (string.IsNullOrEmpty(path))
                    continue;

                // Skip Unity meta-adjacent junk and temp copies if any slip into the walk.
                if (path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    continue;

                byte[] data;
                try
                {
                    data = File.ReadAllBytes(path);
                }
                catch (Exception)
                {
                    loadFailures++;
                    continue;
                }

                if (data == null || data.Length == 0)
                    continue;

                FileBlob blob = new FileBlob
                {
                    FullPath = path.Replace('\\', '/'),
                    FileName = Path.GetFileName(path),
                    Data = data
                };

                if (IsYamlHeader(data))
                    textFiles.Add(blob);
                else
                    binaryFiles.Add(blob);
            }
        }

        private static bool IsYamlHeader(byte[] data)
        {
            // Match python: data.lstrip()[:5] == b"%YAML"
            int i = 0;
            while (i < data.Length)
            {
                byte b = data[i];
                if (b == (byte)' ' || b == (byte)'\t' || b == (byte)'\r' || b == (byte)'\n')
                {
                    i++;
                    continue;
                }

                break;
            }

            if (i + 5 > data.Length)
                return false;

            return data[i] == (byte)'%' &&
                   data[i + 1] == (byte)'Y' &&
                   data[i + 2] == (byte)'A' &&
                   data[i + 3] == (byte)'M' &&
                   data[i + 4] == (byte)'L';
        }

        private static bool IsLivePath(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath))
                return false;

            for (int i = 0; i < NonLiveDirParts.Length; i++)
            {
                string part = NonLiveDirParts[i];
                // Path segment match: /part/ or \part\ or starts/ends with part.
                if (fullPath.IndexOf("/" + part + "/", StringComparison.OrdinalIgnoreCase) >= 0)
                    return false;
                if (fullPath.IndexOf("\\" + part + "\\", StringComparison.OrdinalIgnoreCase) >= 0)
                    return false;
            }

            return true;
        }

        private static void CountGuidHits(
            string guid,
            List<FileBlob> textFiles,
            List<FileBlob> binaryFiles,
            out int live,
            out int other)
        {
            live = 0;
            other = 0;
            byte[] lower = Encoding.ASCII.GetBytes(guid.ToLowerInvariant());
            byte[] upper = Encoding.ASCII.GetBytes(guid.ToUpperInvariant());
            byte[] raw = HexToBytes(guid);
            byte[] swapped = NibbleSwap(raw);

            for (int i = 0; i < textFiles.Count; i++)
            {
                byte[] data = textFiles[i].Data;
                if (IndexOfBytes(data, lower) >= 0 || IndexOfBytes(data, upper) >= 0)
                {
                    if (IsLivePath(textFiles[i].FullPath))
                        live++;
                    else
                        other++;
                }
            }

            for (int i = 0; i < binaryFiles.Count; i++)
            {
                byte[] data = binaryFiles[i].Data;
                if (IndexOfBytes(data, swapped) >= 0 || IndexOfBytes(data, raw) >= 0)
                {
                    if (IsLivePath(binaryFiles[i].FullPath))
                        live++;
                    else
                        other++;
                }
            }
        }

        private static void CollectGuidHitPaths(
            string guid,
            List<FileBlob> textFiles,
            List<FileBlob> binaryFiles,
            List<string> livePaths,
            List<string> otherPaths)
        {
            byte[] lower = Encoding.ASCII.GetBytes(guid.ToLowerInvariant());
            byte[] upper = Encoding.ASCII.GetBytes(guid.ToUpperInvariant());
            byte[] raw = HexToBytes(guid);
            byte[] swapped = NibbleSwap(raw);

            for (int i = 0; i < textFiles.Count; i++)
            {
                byte[] data = textFiles[i].Data;
                if (IndexOfBytes(data, lower) >= 0 || IndexOfBytes(data, upper) >= 0)
                {
                    if (IsLivePath(textFiles[i].FullPath))
                        livePaths.Add(textFiles[i].FullPath);
                    else
                        otherPaths.Add(textFiles[i].FullPath);
                }
            }

            for (int i = 0; i < binaryFiles.Count; i++)
            {
                byte[] data = binaryFiles[i].Data;
                if (IndexOfBytes(data, swapped) >= 0 || IndexOfBytes(data, raw) >= 0)
                {
                    if (IsLivePath(binaryFiles[i].FullPath))
                        livePaths.Add(binaryFiles[i].FullPath);
                    else
                        otherPaths.Add(binaryFiles[i].FullPath);
                }
            }
        }

        private static byte[] HexToBytes(string hex)
        {
            if (string.IsNullOrEmpty(hex) || (hex.Length & 1) != 0)
                return Array.Empty<byte>();

            // COLD ALLOC: guid raw 16 bytes - editor audit only.
            byte[] raw = new byte[hex.Length / 2];
            for (int i = 0; i < raw.Length; i++)
            {
                int hi = HexNibble(hex[i * 2]);
                int lo = HexNibble(hex[i * 2 + 1]);
                if (hi < 0 || lo < 0)
                    return Array.Empty<byte>();
                raw[i] = (byte)((hi << 4) | lo);
            }

            return raw;
        }

        private static int HexNibble(char c)
        {
            if (c >= '0' && c <= '9')
                return c - '0';
            if (c >= 'a' && c <= 'f')
                return 10 + (c - 'a');
            if (c >= 'A' && c <= 'F')
                return 10 + (c - 'A');
            return -1;
        }

        private static byte[] NibbleSwap(byte[] raw)
        {
            if (raw == null || raw.Length == 0)
                return Array.Empty<byte>();

            // COLD ALLOC: nibble-swapped guid - editor audit only.
            byte[] swapped = new byte[raw.Length];
            for (int i = 0; i < raw.Length; i++)
            {
                byte b = raw[i];
                swapped[i] = (byte)(((b & 0x0F) << 4) | (b >> 4));
            }

            return swapped;
        }

        private static int IndexOfBytes(byte[] haystack, byte[] needle)
        {
            if (haystack == null || needle == null || needle.Length == 0 || haystack.Length < needle.Length)
                return -1;

            int limit = haystack.Length - needle.Length;
            for (int i = 0; i <= limit; i++)
            {
                bool match = true;
                for (int j = 0; j < needle.Length; j++)
                {
                    if (haystack[i + j] != needle[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                    return i;
            }

            return -1;
        }

        private struct FileBlob
        {
            public string FullPath;
            public string FileName;
            public byte[] Data;
        }
    }
}
#endif
