using System;
using System.IO;
using UnityEngine;

namespace Hecton8.Core
{
    public static class HectonPersistentPathPolicy
    {
        public static string RootPath
        {
            get
            {
                string root = Application.persistentDataPath;
                return string.IsNullOrEmpty(root) ? "." : root;
            }
        }

        public static string CombineFile(string fileName)
        {
            return Path.Combine(RootPath, NormalizeRelativeSegment(fileName));
        }

        public static string CombineDirectory(string directoryName)
        {
            return Path.Combine(RootPath, NormalizeRelativeSegment(directoryName));
        }

        public static void EnsureParentDirectory(string absoluteFilePath)
        {
            string directory = Path.GetDirectoryName(absoluteFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
        }

        private static string NormalizeRelativeSegment(string segment)
        {
            if (string.IsNullOrEmpty(segment))
                return string.Empty;

            string normalized = segment
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            return normalized.IndexOf("..", StringComparison.Ordinal) >= 0
                ? Path.GetFileName(normalized)
                : normalized;
        }
    }
}
