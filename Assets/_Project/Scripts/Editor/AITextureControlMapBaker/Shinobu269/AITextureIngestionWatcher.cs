#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Hecton8.Editor.AITextureControlMaps
{
    [InitializeOnLoad]
    internal static class AITextureIngestionWatcher
    {
        private const double InitialRetryDelaySeconds = 0.25;
        private const double RetryBackoffSeconds = 0.50;
        private const int MaxReadinessAttempts = 40;

        private static readonly object Gate = new object();
        private static readonly List<PendingInboxImport> PendingImports = new List<PendingInboxImport>(64); // COLD ALLOC: List<PendingInboxImport>[64] - editor inbox watcher queue - owner: AITextureIngestionWatcher
        private static readonly List<PendingInboxImport> ScratchImports = new List<PendingInboxImport>(64); // COLD ALLOC: List<PendingInboxImport>[64] - editor inbox main-thread drain scratch - owner: AITextureIngestionWatcher
        private static FileSystemWatcher _watcher;
        private static bool _drainRegistered;

        static AITextureIngestionWatcher()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= Release;
            AssemblyReloadEvents.beforeAssemblyReload += Release;
            EditorApplication.quitting -= Release;
            EditorApplication.quitting += Release;
        }

        [MenuItem("Hecton8/AI Texture Control Maps/Start AI Texture Inbox Watcher", false, 2680)]
        internal static void StartWatcher()
        {
            StopWatcher();
            string absoluteInbox = Path.Combine(Directory.GetCurrentDirectory(), AITextureControlMapConstants.InboxFolder.Replace('/', Path.DirectorySeparatorChar));
            if (!TryEnsureInboxDirectoryNoThrow(absoluteInbox))
                return;

            FileSystemWatcher watcher = TryStartInboxWatcherNoThrow(absoluteInbox);
            if (watcher == null)
                return;

            _watcher = watcher;
            EnsureDrainRegistered();
        }

        [MenuItem("Hecton8/AI Texture Control Maps/Stop AI Texture Inbox Watcher", false, 2681)]
        internal static void StopWatcher()
        {
            if (_watcher == null)
                return;

            FileSystemWatcher watcher = _watcher;
            _watcher = null;
            StopInboxWatcherNoThrow(watcher);
        }

        [MenuItem("Hecton8/AI Texture Control Maps/Process AI Texture Inbox Now", false, 2682)]
        internal static void ProcessInboxNow()
        {
            string absoluteInbox = Path.Combine(Directory.GetCurrentDirectory(), AITextureControlMapConstants.InboxFolder.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(absoluteInbox))
                Directory.CreateDirectory(absoluteInbox);

            foreach (string file in Directory.EnumerateFiles(absoluteInbox, "*.png", SearchOption.AllDirectories))
            {
                lock (Gate)
                    EnqueuePendingImport(BuildPendingImport(file, 0.0, 0));
            }

            EnsureDrainRegistered();
        }

        private static void OnFileChanged(object sender, FileSystemEventArgs args)
        {
            QueuePath(args.FullPath);
        }

        private static void OnFileRenamed(object sender, RenamedEventArgs args)
        {
            QueuePath(args.FullPath);
        }

        private static void QueuePath(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath) || !absolutePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                return;

            lock (Gate)
                EnqueuePendingImport(BuildPendingImport(absolutePath, InitialRetryDelaySeconds, 0));
        }

        private static void DrainPendingImports()
        {
            ScratchImports.Clear();
            lock (Gate)
            {
                for (int i = 0; i < PendingImports.Count; i++)
                    ScratchImports.Add(PendingImports[i]);
                PendingImports.Clear();
            }

            if (ScratchImports.Count == 0)
            {
                UnregisterDrainIfIdleAfterStop();
                return;
            }

            int imported = 0;
            long nowTicks = Stopwatch.GetTimestamp();
            for (int i = 0; i < ScratchImports.Count; i++)
            {
                PendingInboxImport item = ScratchImports[i];
                if (item.NotBeforeTicks > nowTicks)
                {
                    Requeue(item);
                    continue;
                }

                InboxCopyResult result = CopyIntoProjectIfReady(item.AbsolutePath);
                if (result == InboxCopyResult.Imported)
                {
                    imported++;
                    continue;
                }

                if (result == InboxCopyResult.Retry && item.Attempts < MaxReadinessAttempts)
                {
                    item.Attempts++;
                    item.NotBeforeTicks = DelayToTimestamp(RetryBackoffSeconds);
                    Requeue(item);
                }
            }

            ScratchImports.Clear();
            if (imported > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Hecton8.Core.H8Debug.Log("[AITextureIngestionWatcher] Imported AI textures=" + imported + ".");
            }

            UnregisterDrainIfIdleAfterStop();
        }

        private static void EnsureDrainRegistered()
        {
            if (_drainRegistered)
                return;

            EditorApplication.update -= DrainPendingImports;
            EditorApplication.update += DrainPendingImports;
            _drainRegistered = true;
        }

        private static void Release()
        {
            StopWatcher();
            if (_drainRegistered)
            {
                EditorApplication.update -= DrainPendingImports;
                _drainRegistered = false;
            }
        }

        private static bool TryEnsureInboxDirectoryNoThrow(string absoluteInbox)
        {
            try
            {
                if (!Directory.Exists(absoluteInbox))
                    Directory.CreateDirectory(absoluteInbox);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
            catch (System.Security.SecurityException)
            {
                return false;
            }
        }

        private static FileSystemWatcher TryStartInboxWatcherNoThrow(string absoluteInbox)
        {
            try
            {
                FileSystemWatcher watcher = new FileSystemWatcher(absoluteInbox, "*.png")
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime
                };
                watcher.Created += OnFileChanged;
                watcher.Changed += OnFileChanged;
                watcher.Renamed += OnFileRenamed;
                watcher.EnableRaisingEvents = true;
                return watcher;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (NotSupportedException)
            {
                return null;
            }
            catch (System.Security.SecurityException)
            {
                return null;
            }
        }

        private static void StopInboxWatcherNoThrow(FileSystemWatcher watcher)
        {
            try
            {
                watcher.EnableRaisingEvents = false;
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
            catch (IOException)
            {
            }

            watcher.Created -= OnFileChanged;
            watcher.Changed -= OnFileChanged;
            watcher.Renamed -= OnFileRenamed;

            try
            {
                watcher.Dispose();
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
            catch (IOException)
            {
            }
        }

        private static InboxCopyResult CopyIntoProjectIfReady(string absoluteSourcePath)
        {
            if (string.IsNullOrEmpty(absoluteSourcePath) || !File.Exists(absoluteSourcePath))
                return InboxCopyResult.Failed;

            FileInfo info = new FileInfo(absoluteSourcePath);
            if (info.Length <= 0)
                return InboxCopyResult.Retry;
            if (!CanReadExclusive(absoluteSourcePath))
                return InboxCopyResult.Retry;

            EnsureAssetFolder("Assets/_Project");
            EnsureAssetFolder("Assets/_Project/Textures");
            EnsureAssetFolder(AITextureControlMapConstants.ImportedTextureFolder);
            string fileName = Path.GetFileName(absoluteSourcePath);
            string targetAssetPath = AITextureControlMapConstants.ImportedTextureFolder + "/" + fileName;
            string absoluteTargetPath = Path.Combine(Directory.GetCurrentDirectory(), targetAssetPath.Replace('/', Path.DirectorySeparatorChar));
            try
            {
                File.Copy(absoluteSourcePath, absoluteTargetPath, true);
                AssetDatabase.ImportAsset(targetAssetPath, ImportAssetOptions.ForceUpdate);
                return InboxCopyResult.Imported;
            }
            catch (IOException)
            {
                return InboxCopyResult.Retry;
            }
            catch (UnauthorizedAccessException)
            {
                return InboxCopyResult.Retry;
            }
        }

        private static bool CanReadExclusive(string absoluteSourcePath)
        {
            try
            {
                using (FileStream stream = new FileStream(absoluteSourcePath, FileMode.Open, FileAccess.Read, FileShare.None, 4096, FileOptions.SequentialScan))
                    return stream.Length > 0;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static void Requeue(PendingInboxImport item)
        {
            lock (Gate)
                EnqueuePendingImport(item);
        }

        private static void EnqueuePendingImport(PendingInboxImport item)
        {
            for (int i = 0; i < PendingImports.Count; i++)
            {
                PendingInboxImport existing = PendingImports[i];
                if (!string.Equals(existing.AbsolutePath, item.AbsolutePath, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (item.NotBeforeTicks < existing.NotBeforeTicks)
                    existing.NotBeforeTicks = item.NotBeforeTicks;
                if (item.Attempts > existing.Attempts)
                    existing.Attempts = item.Attempts;
                PendingImports[i] = existing;
                return;
            }

            PendingImports.Add(item);
        }

        private static PendingInboxImport BuildPendingImport(string absolutePath, double delaySeconds, int attempts)
        {
            PendingInboxImport item;
            item.AbsolutePath = absolutePath;
            item.NotBeforeTicks = DelayToTimestamp(delaySeconds);
            item.Attempts = attempts;
            return item;
        }

        private static long DelayToTimestamp(double delaySeconds)
        {
            long now = Stopwatch.GetTimestamp();
            if (delaySeconds <= 0.0)
                return now;

            double ticks = delaySeconds * Stopwatch.Frequency;
            long remaining = long.MaxValue - now;
            if (ticks >= remaining)
                return long.MaxValue;

            return now + (long)ticks;
        }

        private static void UnregisterDrainIfIdleAfterStop()
        {
            if (_watcher != null || !_drainRegistered)
                return;

            lock (Gate)
            {
                if (PendingImports.Count > 0)
                    return;
            }

            EditorApplication.update -= DrainPendingImports;
            _drainRegistered = false;
        }

        private static void EnsureAssetFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
                return;

            int slash = assetPath.LastIndexOf('/');
            if (slash <= 0)
                return;

            string parent = assetPath.Substring(0, slash);
            string folder = assetPath.Substring(slash + 1);
            EnsureAssetFolder(parent);
            if (!AssetDatabase.IsValidFolder(assetPath))
                AssetDatabase.CreateFolder(parent, folder);
        }

        private enum InboxCopyResult
        {
            Failed = 0,
            Retry = 1,
            Imported = 2
        }

        private struct PendingInboxImport
        {
            public string AbsolutePath;
            public long NotBeforeTicks;
            public int Attempts;
        }
    }
}
#endif
