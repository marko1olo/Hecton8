#if UNITY_EDITOR
using System.Globalization;
using System.IO;
using System.Text;
using Hecton8.Core.Memory;
using Hecton8.Data;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.Validation
{
    /// <summary>
    /// Lightweight batchmode-safe CI proof for Data Monolith runtime boot:
    /// file present → file checksum (TryValidateBlobFile) → GlobalDataVault arena load → header gate.
    ///
    /// Complements H8DataMonolithGlobalDataVaultStressProbe (heavy hard-exit stress suite).
    /// Soft FAIL stays exit 0 under -quit. Does not bake or mutate the blob.
    /// </summary>
    public static class H8DataMonolithRuntimeBootValidator
    {
        private const string LogPrefix = "[H8DataMonolithRuntimeBootValidator]";
        private const string BlobAssetPath = "Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin";

        // COLD ALLOC: StringBuilder[4096] - editor audit report builder - owner: H8DataMonolithRuntimeBootValidator
        private static readonly StringBuilder Report = new StringBuilder(4096);

        /// <summary>
        /// Public for -executeMethod / CI batchmode. Soft FAIL stays exit 0 under -quit.
        /// Hard exit 1 only when the blob path is missing on disk (infrastructure gap).
        /// </summary>
        [MenuItem("Hecton8/Validation/Validate Data Monolith Runtime Boot", priority = 187)]
        public static void ValidateDataMonolithRuntimeBoot()
        {
            bool batch = Application.isBatchMode;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating || Application.isPlaying)
            {
                string busy = LogPrefix + " RESULT: FAIL — Editor busy (compiling/updating/playing).";
                Debug.LogError(busy);
                if (!batch)
                    EditorUtility.DisplayDialog("Data Monolith Runtime Boot", busy, "OK");
                return;
            }

            Report.Clear();
            Report.AppendLine("═══════════════════════════════════════════════════════");
            Report.AppendLine("HECTON-8 — Data Monolith Runtime Boot Audit");
            Report.AppendLine("═══════════════════════════════════════════════════════");
            Report.AppendLine();

            string projectRoot = ResolveProjectRoot();
            string blobPath = Path.Combine(projectRoot, BlobAssetPath.Replace('/', Path.DirectorySeparatorChar));
            bool fileExists = File.Exists(blobPath);
            long fileBytes = 0L;
            if (fileExists)
            {
                try
                {
                    fileBytes = new FileInfo(blobPath).Length;
                }
                catch (IOException)
                {
                    fileBytes = -1L;
                }
                catch (System.UnauthorizedAccessException)
                {
                    fileBytes = -1L;
                }
            }

            Report.Append("blobPath=").Append(NormalizePath(blobPath)).AppendLine();
            Report.Append("fileExists=").Append(fileExists ? 1 : 0);
            Report.Append(" fileBytes=").Append(fileBytes.ToString(CultureInfo.InvariantCulture));
            Report.AppendLine();

            if (!fileExists)
            {
                string missing = LogPrefix + " RESULT: FAIL — static_data.h8bin missing on disk: " + NormalizePath(blobPath);
                Debug.LogError(missing);
                Debug.LogError(Report.ToString());
                if (!batch)
                    EditorUtility.DisplayDialog("Data Monolith Runtime Boot", missing, "OK");
                if (batch)
                    EditorApplication.Exit(1);
                return;
            }

            // 1) File-level checksum / structural validation (no vault).
            bool checksumOk = H8DataMonolithCompiler.TryValidateBlobFile(blobPath, out string checksumError);
            if (string.IsNullOrEmpty(checksumError))
                checksumError = string.Empty;

            Report.Append("checksumOk=").Append(checksumOk ? 1 : 0);
            if (!checksumOk)
                Report.Append(" checksumError=").Append(checksumError);
            Report.AppendLine();

            // 2) Runtime arena boot via GlobalDataVault (player-equivalent path).
            bool loadOk = false;
            bool isLoaded = false;
            H8DataBlobLoadStatus loadStatus = H8DataBlobLoadStatus.None;
            uint magic = 0u;
            ushort formatVersion = 0;
            ulong checksum64 = 0UL;
            uint blobBytesHeader = 0u;
            int arenaByteLength = 0;
            bool magicOk = false;
            bool versionOk = false;
            string loadError = string.Empty;

            H8StaticDataArena.Shutdown();
            try
            {
                using GlobalDataVault vault = GlobalDataVault.Create();
                // Match StressProbe RunFileLoadProof: vault, path, appVersionHash=0, worldSeed=0, failIfMissing=false.
                loadOk = H8StaticDataArena.TryInitializeFromFile(vault, blobPath, 0u, 0u, false, out loadStatus);
                isLoaded = H8StaticDataArena.IsLoaded;

                if (isLoaded)
                {
                    H8DataBlobHeader header = H8StaticDataArena.Header;
                    magic = header.Magic;
                    formatVersion = header.FormatVersion;
                    checksum64 = header.Checksum64;
                    blobBytesHeader = header.BlobBytes;
                    arenaByteLength = H8StaticDataArena.ByteLength;
                    magicOk = magic == H8DataLayoutConstants.BlobMagic;
                    versionOk = formatVersion == H8DataLayoutConstants.FormatVersion;
                }
            }
            catch (System.Exception ex)
            {
                loadOk = false;
                isLoaded = false;
                loadError = ex.GetType().Name + ": " + ex.Message;
            }
            finally
            {
                H8StaticDataArena.Shutdown();
            }

            Report.Append("loadOk=").Append(loadOk ? 1 : 0);
            Report.Append(" loadStatus=").Append(loadStatus.ToString());
            Report.Append(" isLoaded=").Append(isLoaded ? 1 : 0);
            Report.Append(" magicOk=").Append(magicOk ? 1 : 0);
            Report.Append(" versionOk=").Append(versionOk ? 1 : 0);
            Report.Append(" magic=0x").Append(magic.ToString("X8", CultureInfo.InvariantCulture));
            Report.Append(" formatVersion=").Append(formatVersion.ToString(CultureInfo.InvariantCulture));
            Report.Append(" checksum64=0x").Append(checksum64.ToString("X16", CultureInfo.InvariantCulture));
            Report.Append(" headerBlobBytes=").Append(blobBytesHeader.ToString(CultureInfo.InvariantCulture));
            Report.Append(" arenaByteLength=").Append(arenaByteLength.ToString(CultureInfo.InvariantCulture));
            if (!string.IsNullOrEmpty(loadError))
                Report.Append(" loadError=").Append(loadError);
            Report.AppendLine();

            bool statusLoaded = loadStatus == H8DataBlobLoadStatus.Loaded;
            bool passed = checksumOk &&
                          loadOk &&
                          statusLoaded &&
                          isLoaded &&
                          magicOk &&
                          versionOk &&
                          arenaByteLength > 0 &&
                          blobBytesHeader > 0u;

            Report.AppendLine();
            Report.Append(LogPrefix).Append(" RESULT: ").Append(passed ? "PASS" : "FAIL");
            Report.Append(" checksumOk=").Append(checksumOk ? 1 : 0);
            Report.Append(" loadOk=").Append(loadOk ? 1 : 0);
            Report.Append(" statusLoaded=").Append(statusLoaded ? 1 : 0);
            Report.Append(" isLoaded=").Append(isLoaded ? 1 : 0);
            Report.Append(" magicOk=").Append(magicOk ? 1 : 0);
            Report.Append(" versionOk=").Append(versionOk ? 1 : 0);
            Report.Append(" fileBytes=").Append(fileBytes.ToString(CultureInfo.InvariantCulture));
            Report.AppendLine();

            string reportText = Report.ToString();
            if (passed)
                Debug.Log(reportText);
            else
                Debug.LogError(reportText);

            if (!batch)
            {
                EditorUtility.DisplayDialog(
                    "Data Monolith Runtime Boot",
                    passed
                        ? "PASS — static_data.h8bin checksum + GlobalDataVault arena boot OK."
                        : "FAIL — see Console for measured fields.",
                    "OK");
            }

            // Soft FAIL under -quit: do not EditorApplication.Exit on audit fail.
            // Missing blob already hard-exited above.
        }

        private static string ResolveProjectRoot()
        {
            return Directory.GetParent(Application.dataPath)?.FullName ?? Directory.GetCurrentDirectory();
        }

        private static string NormalizePath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/');
        }
    }
}
#endif
