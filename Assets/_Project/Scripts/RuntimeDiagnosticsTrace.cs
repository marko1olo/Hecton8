// ============================================================================
// HECTON-8 - RuntimeDiagnosticsTrace.cs
// File-backed runtime diagnostics trace for long profiling sessions.
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Hecton8.Core;
using UnityEngine;

namespace Hecton8.Dev
{
    /// <summary>
    /// Writes high-signal runtime diagnostics events into a session file.
    /// </summary>
    public static class RuntimeDiagnosticsTrace
    {
        private const int MaxSessionBytes = 256 * 1024;
        private static readonly object _Gate = new object();
        private static StreamWriter _writer;
        private static string _currentFilePath = string.Empty;
        private static long _approximateBytesWritten;
        private static bool _sizeLimitReached;
        private static bool _startupLogged;
        private static readonly Dictionary<string, string> _lastMessageByChannel = new Dictionary<string, string>(8);
        private static readonly Dictionary<string, int> _suppressedDuplicateCountByChannel = new Dictionary<string, int>(8);
        private static readonly List<string> _suppressedDuplicateFlushChannels = new List<string>(8);
        private static readonly char[] _invalidFileNameChars = Path.GetInvalidFileNameChars();

        /// <summary>
        /// Gets the absolute path of the current diagnostics file.
        /// </summary>
        public static string CurrentFilePath => _currentFilePath;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            lock (_Gate)
            {
                if (_writer != null)
                {
                    _writer.Dispose();
                    _writer = null;
                }

                _currentFilePath = string.Empty;
                _approximateBytesWritten = 0L;
                _sizeLimitReached = false;
                _startupLogged = false;
                _lastMessageByChannel.Clear();
                _suppressedDuplicateCountByChannel.Clear();
                _suppressedDuplicateFlushChannels.Clear();
            }
        }

        /// <summary>
        /// Gets whether the trace currently has an open output file.
        /// </summary>
        public static bool IsActive
        {
            get
            {
                lock (_Gate)
                    return _writer != null;
            }
        }

        /// <summary>
        /// Opens a new session file if one is not already active.
        /// </summary>
        public static void EnsureSession(string sessionLabel)
        {
            lock (_Gate)
            {
                if (_writer != null)
                    return;

                string safeLabel = string.IsNullOrWhiteSpace(sessionLabel) ? "runtime" : sessionLabel.Trim();
                for (int i = 0; i < _invalidFileNameChars.Length; i++)
                {
                    char invalidChar = _invalidFileNameChars[i];
                    safeLabel = safeLabel.Replace(invalidChar, '_');
                }

                string directory = HectonPersistentPathPolicy.CombineDirectory("Diagnostics");
                Directory.CreateDirectory(directory);

                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                _currentFilePath = Path.Combine(directory, $"Hecton8_{safeLabel}_{timestamp}.log");
                _writer = new StreamWriter(_currentFilePath, false, Encoding.UTF8, 4096)
                {
                    AutoFlush = true
                };

                _writer.WriteLine("# HECTON-8 Runtime Diagnostics Trace");
                _writer.WriteLine($"# started={DateTime.Now:O}");
                _writer.WriteLine($"# unityVersion={Application.unityVersion}");
                _writer.WriteLine($"# product={Application.productName}");
                _writer.WriteLine($"# persistentDataPath={Application.persistentDataPath}");
                _approximateBytesWritten = 0L;
                _sizeLimitReached = false;
                _startupLogged = false;
                _lastMessageByChannel.Clear();
                _suppressedDuplicateCountByChannel.Clear();
                _suppressedDuplicateFlushChannels.Clear();
            }
        }

        /// <summary>
        /// Closes the active diagnostics file.
        /// </summary>
        public static void CloseSession()
        {
            lock (_Gate)
            {
                if (_writer == null)
                    return;

                try
                {
                    FlushSuppressedDuplicates();
                    _writer.WriteLine($"# closed={DateTime.Now:O}");
                    _writer.Flush();
                    _writer.Dispose();
                }
                finally
                {
                    _writer = null;
                    _currentFilePath = string.Empty;
                    _approximateBytesWritten = 0L;
                    _sizeLimitReached = false;
                    _startupLogged = false;
                    _lastMessageByChannel.Clear();
                    _suppressedDuplicateCountByChannel.Clear();
                    _suppressedDuplicateFlushChannels.Clear();
                }
            }
        }

        /// <summary>
        /// Writes a single diagnostics event line.
        /// </summary>
        public static void WriteEvent(string channel, string message)
        {
            lock (_Gate)
            {
                if (_writer == null)
                    return;

                if (_sizeLimitReached)
                    return;

                if (!_startupLogged)
                {
                    _writer.WriteLine($"# file={_currentFilePath}");
                    _startupLogged = true;
                }

                string safeChannel = string.IsNullOrWhiteSpace(channel) ? "runtime" : channel.Trim();
                string safeMessage = string.IsNullOrWhiteSpace(message) ? "empty" : message.Replace('\r', ' ').Replace('\n', ' ');

                if (ShouldSuppressDuplicate(safeChannel, safeMessage))
                    return;

                FlushSuppressedDuplicatesForChannel(safeChannel);

                string line = BuildLine(safeChannel, safeMessage);
                if (!TryWriteLine(line))
                    return;

                _lastMessageByChannel[safeChannel] = safeMessage;
            }
        }

        private static bool ShouldSuppressDuplicate(string channel, string message)
        {
            if (!string.Equals(channel, "render.audit", StringComparison.Ordinal))
                return false;

            if (!_lastMessageByChannel.TryGetValue(channel, out string previousMessage))
                return false;

            if (!string.Equals(previousMessage, message, StringComparison.Ordinal))
                return false;

            if (_suppressedDuplicateCountByChannel.TryGetValue(channel, out int suppressedCount))
                _suppressedDuplicateCountByChannel[channel] = suppressedCount + 1;
            else
                _suppressedDuplicateCountByChannel[channel] = 1;

            return true;
        }

        private static void FlushSuppressedDuplicates()
        {
            if (_suppressedDuplicateCountByChannel.Count == 0)
                return;

            _suppressedDuplicateFlushChannels.Clear();
            Dictionary<string, int>.Enumerator enumerator = _suppressedDuplicateCountByChannel.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<string, int> pair = enumerator.Current;
                if (pair.Value > 0)
                    _suppressedDuplicateFlushChannels.Add(pair.Key);
            }

            for (int i = 0; i < _suppressedDuplicateFlushChannels.Count; i++)
                FlushSuppressedDuplicatesForChannel(_suppressedDuplicateFlushChannels[i]);

            _suppressedDuplicateFlushChannels.Clear();
        }

        private static void FlushSuppressedDuplicatesForChannel(string channel)
        {
            if (!_suppressedDuplicateCountByChannel.TryGetValue(channel, out int suppressedCount) || suppressedCount <= 0)
                return;

            string line = BuildLine(channel, $"duplicate snapshot suppressed x{suppressedCount}");
            if (TryWriteLine(line))
                _suppressedDuplicateCountByChannel[channel] = 0;
        }

        private static string BuildLine(string channel, string message)
        {
            StringBuilder builder = new StringBuilder(192);
            builder.Append(DateTime.Now.ToString("HH:mm:ss.fff"));
            builder.Append(" | frame=");
            builder.Append(SystemDispatcher.CurrentFrameIndex);
            builder.Append(" | t=");
            builder.Append(Time.realtimeSinceStartup.ToString("0.000"));
            builder.Append(" | ");
            builder.Append(channel);
            builder.Append(" | ");
            builder.Append(message);
            return builder.ToString();
        }

        private static bool TryWriteLine(string line)
        {
            if (_writer == null)
                return false;

            int bytesToWrite = Encoding.UTF8.GetByteCount(line) + 2;
            if (_approximateBytesWritten + bytesToWrite > MaxSessionBytes)
            {
                string stopLine = $"# trace-size-limit reached at {DateTime.Now:O} maxBytes={MaxSessionBytes}";
                _writer.WriteLine(stopLine);
                _writer.Flush();
                _approximateBytesWritten += Encoding.UTF8.GetByteCount(stopLine) + 2;
                _sizeLimitReached = true;
                return false;
            }

            _writer.WriteLine(line);
            _approximateBytesWritten += bytesToWrite;
            return true;
        }
    }
}
