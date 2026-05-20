using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace Hecton8.World.OfflineWreckageBaker.Editor
{
    internal static class OfflineWreckageAtomicFile
    {
        private static readonly int s_processId = ResolveProcessId();
        private static int s_tempOrdinal;

        public static string CreateTempPath(string finalPath)
        {
            int ordinal = Interlocked.Increment(ref s_tempOrdinal);
            return finalPath + ".tmp." + s_processId.ToString(CultureInfo.InvariantCulture) + "." + ordinal.ToString(CultureInfo.InvariantCulture);
        }

        public static void Publish(string tempPath, string finalPath)
        {
            if (File.Exists(finalPath))
                File.Replace(tempPath, finalPath, null);
            else
                File.Move(tempPath, finalPath);
        }

        public static void WriteBytes(string finalPath, ReadOnlySpan<byte> bytes)
        {
            EnsureDirectory(finalPath);
            string tempPath = CreateTempPath(finalPath);
            try
            {
                using (FileStream stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    stream.Write(bytes);
                Publish(tempPath, finalPath);
                tempPath = null;
            }
            finally
            {
                DeleteOwnedTemp(tempPath);
            }
        }

        public static void WriteTextUtf8(string finalPath, string text)
        {
            EnsureDirectory(finalPath);
            string tempPath = CreateTempPath(finalPath);
            try
            {
                using (FileStream stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    writer.Write(text);
                }

                Publish(tempPath, finalPath);
                tempPath = null;
            }
            finally
            {
                DeleteOwnedTemp(tempPath);
            }
        }

        public static void DeleteOwnedTemp(string ownedTempPath)
        {
            if (string.IsNullOrEmpty(ownedTempPath) || !File.Exists(ownedTempPath))
                return;

            try
            {
                File.Delete(ownedTempPath);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static int ResolveProcessId()
        {
            using (Process process = Process.GetCurrentProcess())
                return process.Id;
        }

        private static void EnsureDirectory(string finalPath)
        {
            string directory = Path.GetDirectoryName(finalPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
        }
    }
}
