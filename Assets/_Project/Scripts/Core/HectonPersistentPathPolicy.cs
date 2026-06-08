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
            return CombineUnderRoot(RootPath, fileName);
        }

        public static string CombineFile(string rootPath, string fileName)
        {
            return CombineUnderRoot(string.IsNullOrEmpty(rootPath) ? RootPath : rootPath, fileName);
        }

        public static string CombineDirectory(string directoryName)
        {
            return CombineUnderRoot(RootPath, directoryName);
        }

        public static void EnsureParentDirectory(string absoluteFilePath)
        {
            EnsureParentDirectoryCold(absoluteFilePath);
        }

        public static void EnsureParentDirectoryCold(string absoluteFilePath)
        {
            string directory = Path.GetDirectoryName(absoluteFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
        }

        private static string NormalizeRelativeSegment(string segment)
        {
            if (string.IsNullOrEmpty(segment))
                return string.Empty;

            string replaced = segment
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);

            bool unsafePath = Path.IsPathRooted(replaced) ||
                              HasVolumeSeparator(replaced) ||
                              ContainsParentTraversal(replaced);

            string normalized = replaced.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return unsafePath ? SanitizeFileName(Path.GetFileName(normalized)) : normalized;
        }

        private static string CombineUnderRoot(string rootPath, string segment)
        {
            string root = ResolveFullPath(string.IsNullOrEmpty(rootPath) ? "." : rootPath, ".");
            string normalized = NormalizeRelativeSegment(segment);
            string candidate = ResolveFullPath(Path.Combine(root, normalized), root);
            if (IsUnderRoot(candidate, root))
                return candidate;

            string fallback = SanitizeFileName(Path.GetFileName(normalized));
            return ResolveFullPath(Path.Combine(root, fallback), root);
        }

        private static string ResolveFullPath(string path, string fallback)
        {
            try
            {
                return Path.GetFullPath(path);
            }
            catch (ArgumentException)
            {
                return fallback;
            }
            catch (NotSupportedException)
            {
                return fallback;
            }
            catch (PathTooLongException)
            {
                return fallback;
            }
        }

        private static bool IsUnderRoot(string candidate, string root)
        {
            string normalizedRoot = EnsureTrailingSeparator(root);
            string normalizedCandidate = EnsureTrailingSeparator(candidate);
            return normalizedCandidate.StartsWith(normalizedRoot, PathComparison);
        }

        private static string EnsureTrailingSeparator(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            char last = path[path.Length - 1];
            if (last == Path.DirectorySeparatorChar || last == Path.AltDirectorySeparatorChar)
                return path;

            return path + Path.DirectorySeparatorChar;
        }

        private static StringComparison PathComparison
        {
            get { return Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal; }
        }

        private static bool HasVolumeSeparator(string segment)
        {
            for (int i = 0; i < segment.Length; i++)
            {
                if (segment[i] == ':')
                    return true;
            }

            return false;
        }

        private static bool ContainsParentTraversal(string segment)
        {
            int start = 0;
            while (start <= segment.Length)
            {
                int separator = segment.IndexOf(Path.DirectorySeparatorChar, start);
                int length = separator < 0 ? segment.Length - start : separator - start;
                if (length == 2 && segment[start] == '.' && segment[start + 1] == '.')
                    return true;

                if (separator < 0)
                    return false;

                start = separator + 1;
            }

            return false;
        }

        private static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return string.Empty;

            int colon = fileName.IndexOf(':');
            if (colon == 1 && colon + 1 < fileName.Length)
                fileName = fileName.Substring(colon + 1);

            char[] invalid = Path.GetInvalidFileNameChars();
            char[] sanitized = null;
            for (int i = 0; i < fileName.Length; i++)
            {
                char value = fileName[i];
                bool replace = value == Path.DirectorySeparatorChar ||
                               value == Path.AltDirectorySeparatorChar ||
                               value == ':';

                for (int j = 0; !replace && j < invalid.Length; j++)
                    replace = value == invalid[j];

                if (!replace)
                    continue;

                if (sanitized == null)
                {
                    // COLD ALLOC: char[fileName.Length] - malformed persistent path sanitization - owner: HectonPersistentPathPolicy
                    sanitized = fileName.ToCharArray();
                }

                sanitized[i] = '_';
            }

            if (sanitized == null)
                return fileName;

            // COLD ALLOC: string[1] - sanitized malformed persistent path leaf - owner: HectonPersistentPathPolicy
            return new string(sanitized);
        }
    }
}
