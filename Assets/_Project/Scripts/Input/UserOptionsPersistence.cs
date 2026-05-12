using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Hecton8.Core;
using UnityEngine;

namespace Hecton8.Input
{
    /// <summary>
    /// Central storage owner for user options backed by persistentDataPath/options.h8cfg.
    /// Keeps option persistence out of UI shells and scene controllers.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-30995)]
    public sealed class UserOptionsPersistence : MonoBehaviour, IServiceHeartbeat, IServiceShutdown, IPlatformIntegration
    {
        public const string LanguageKey = "Hecton_Language";
        public const string ScalabilityTierKey = "Hecton_ScalabilityTier";
        public const string FileName = "options.h8cfg";

        private const int FileVersion = 2;
        private const int TypeInt = 1;
        private const int TypeFloat = 2;
        private const int TypeString = 3;
        private const int TypeBool = 4;
        private const int FileMagic = 0x46433848; // H8CF, little endian.
        private const int LegacyFileHeaderBytes = 12;
        private const int FileHeaderBytes = 16;
        private const int MaxOptionsPayloadBytes = 64 * 1024;
        private const long FixedOptionsFileBytes = FileHeaderBytes + MaxOptionsPayloadBytes;
        private const byte DefaultScalabilityTier = ScalabilityTierProfiles.LowMx350;
        private static readonly Encoding OptionsEncoding = new UTF8Encoding(false);

        private readonly Dictionary<string, OptionRecord> _records =
            new Dictionary<string, OptionRecord>(64); // COLD ALLOC: Dictionary<string, OptionRecord>[64] - user options key/value cache - owner: UserOptionsPersistence
        private readonly OptionsFile _optionsFile = new OptionsFile(); // COLD ALLOC: OptionsFile[1] - reusable payload wrapper for options.h8cfg - owner: UserOptionsPersistence

        private OptionRecord[] _writeRecords = Array.Empty<OptionRecord>();
        private readonly byte[] _payloadBuffer = new byte[MaxOptionsPayloadBytes]; // COLD ALLOC: byte[64K] - fixed options.h8cfg payload buffer - owner: UserOptionsPersistence
        private readonly byte[] _headerBuffer = new byte[FileHeaderBytes]; // COLD ALLOC: byte[16] - fixed options.h8cfg header buffer - owner: UserOptionsPersistence
        private string _optionsPath;
        private string _optionsDirectory;
        private byte _scalabilityTier = DefaultScalabilityTier;
        private bool _loaded;
        private bool _serviceRegistered;
        private bool _serviceShuttingDown;
        private bool _serviceShutdownComplete;

        public ServiceHeartbeatState HeartbeatState =>
            _serviceShuttingDown
                ? ServiceHeartbeatState.Shutdown
                : _serviceRegistered
                    ? ServiceHeartbeatState.Ready
                    : ServiceHeartbeatState.NotStarted;

        public bool IsServiceReady => _serviceRegistered && !_serviceShuttingDown;

        public string OptionsPath => ResolveOptionsPath();

        public byte ScalabilityTier
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                EnsureLoaded();
                return _scalabilityTier;
            }
        }

        private void Awake()
        {
            LoadFromDisk();

            BootstrapRegistryBridge.TryResolve(BootstrapRegistryBridgeSlot.UserOptionsRuntime, out UserOptionsPersistence registered);
            if (registered != null && registered != this)
            {
                Destroy(gameObject);
                return;
            }

            RegisterService();
        }

        private void OnEnable()
        {
            RegisterService();
        }

        private void OnDisable()
        {
            if (_serviceRegistered && !_serviceShuttingDown)
                UnregisterService();
        }

        private void OnDestroy()
        {
            OnServiceShutdown();
        }

        public void OnServiceShutdown()
        {
            if (_serviceShutdownComplete)
                return;

            _serviceShuttingDown = true;
            UnregisterService();
            Save();
            _serviceShutdownComplete = true;
        }

        private void RegisterService()
        {
            if (_serviceShuttingDown || !Application.isPlaying)
                return;

            BootstrapRegistryBridge.TryResolve(BootstrapRegistryBridgeSlot.UserOptionsRuntime, out UserOptionsPersistence registered);
            if (registered != null && registered != this)
            {
                Destroy(gameObject);
                return;
            }

            if (!ReferenceEquals(registered, this))
                BootstrapRegistryBridge.Register(BootstrapRegistryBridgeSlot.UserOptionsRuntime, this);

            _serviceRegistered =
                BootstrapRegistryBridge.TryResolve(BootstrapRegistryBridgeSlot.UserOptionsRuntime, out registered) &&
                ReferenceEquals(registered, this);
        }

        private void UnregisterService()
        {
            if (!_serviceRegistered)
                return;

            BootstrapRegistryBridge.Unregister(BootstrapRegistryBridgeSlot.UserOptionsRuntime, this);
            _serviceRegistered = false;
        }

        public bool HasKey(string key)
        {
            EnsureLoaded();
            return !string.IsNullOrWhiteSpace(key) && _records.ContainsKey(key);
        }

        public int GetInt(string key, int defaultValue = 0)
        {
            if (!TryGetRecord(key, out OptionRecord record))
                return defaultValue;

            if (record.Type == TypeInt)
                return record.IntValue;

            if (record.Type == TypeBool)
                return record.BoolValue ? 1 : 0;

            return defaultValue;
        }

        public bool TryGetInt(string key, out int value)
        {
            if (TryGetRecord(key, out OptionRecord record))
            {
                if (record.Type == TypeInt)
                {
                    value = record.IntValue;
                    return true;
                }

                if (record.Type == TypeBool)
                {
                    value = record.BoolValue ? 1 : 0;
                    return true;
                }
            }

            value = default;
            return false;
        }

        public void SetInt(string key, int value)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            EnsureLoaded();
            _records[key] = new OptionRecord
            {
                Key = key,
                Type = TypeInt,
                IntValue = value
            };
        }

        public float GetFloat(string key, float defaultValue = 0f)
        {
            if (!TryGetRecord(key, out OptionRecord record))
                return defaultValue;

            if (record.Type == TypeFloat)
                return record.FloatValue;

            if (record.Type == TypeInt)
                return record.IntValue;

            return defaultValue;
        }

        public bool TryGetFloat(string key, out float value)
        {
            if (TryGetRecord(key, out OptionRecord record))
            {
                if (record.Type == TypeFloat)
                {
                    value = record.FloatValue;
                    return true;
                }

                if (record.Type == TypeInt)
                {
                    value = record.IntValue;
                    return true;
                }
            }

            value = default;
            return false;
        }

        public void SetFloat(string key, float value)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            EnsureLoaded();
            _records[key] = new OptionRecord
            {
                Key = key,
                Type = TypeFloat,
                FloatValue = value
            };
        }

        public string GetString(string key, string defaultValue = "")
        {
            if (!TryGetRecord(key, out OptionRecord record) || record.Type != TypeString)
                return defaultValue ?? string.Empty;

            return record.StringValue ?? string.Empty;
        }

        public bool TryGetString(string key, out string value)
        {
            if (TryGetRecord(key, out OptionRecord record) && record.Type == TypeString)
            {
                value = record.StringValue ?? string.Empty;
                return true;
            }

            value = string.Empty;
            return false;
        }

        public void SetString(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            EnsureLoaded();
            _records[key] = new OptionRecord
            {
                Key = key,
                Type = TypeString,
                StringValue = value ?? string.Empty
            };
        }

        public bool GetBool(string key, bool defaultValue = false)
        {
            if (!TryGetBool(key, out bool value))
                return defaultValue;

            return value;
        }

        public bool TryGetBool(string key, out bool value)
        {
            if (TryGetRecord(key, out OptionRecord record))
            {
                if (record.Type == TypeBool)
                {
                    value = record.BoolValue;
                    return true;
                }

                if (record.Type == TypeInt)
                {
                    value = record.IntValue != 0;
                    return true;
                }
            }

            value = default;
            return false;
        }

        public void SetBool(string key, bool value)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            EnsureLoaded();
            _records[key] = new OptionRecord
            {
                Key = key,
                Type = TypeBool,
                BoolValue = value
            };
        }

        public void DeleteKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            EnsureLoaded();
            _records.Remove(key);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetScalabilityTier(byte tier)
        {
            EnsureLoaded();

            byte normalizedTier = ScalabilityTierProfiles.Normalize(tier);
            byte previousTier = _scalabilityTier;
            if (previousTier == normalizedTier)
                return;

            _scalabilityTier = normalizedTier;
            SyncScalabilityTierRecord();
            PlatformIntegrationBridge.ApplyScalabilityTier(normalizedTier);
            Save();

            PlatformIntegrationBridge.PublishScalabilityChanged(previousTier, normalizedTier);
        }

        public void Save()
        {
            EnsureLoaded();

            string path = ResolveOptionsPath();
            if (!string.IsNullOrEmpty(_optionsDirectory))
                Directory.CreateDirectory(_optionsDirectory);

            SyncScalabilityTierRecord();
            int recordCount = _records.Count;
            if (_writeRecords.Length != recordCount)
                _writeRecords = new OptionRecord[recordCount]; // COLD ALLOC: OptionRecord[count] - resized only when option key count changes - owner: UserOptionsPersistence

            _records.Values.CopyTo(_writeRecords, 0);
            _optionsFile.Version = FileVersion;
            _optionsFile.Records = _writeRecords;

            string tempPath = path + ".tmp";
            WritePortableOptionsFile(tempPath, JsonUtility.ToJson(_optionsFile, false));

            if (File.Exists(path))
                File.Delete(path);

            File.Move(tempPath, path);
        }

        private void EnsureLoaded()
        {
            if (_loaded)
                return;

            LoadFromDisk();
        }

        private void LoadFromDisk()
        {
            if (_loaded)
                return;

            _records.Clear();
            string path = ResolveOptionsPath();
            if (!File.Exists(path))
            {
                ApplyLoadedScalabilityTier(ResolveDefaultScalabilityTier(), true);
                _loaded = true;
                return;
            }

            byte loadedScalabilityTier = ResolveDefaultScalabilityTier();
            bool hasLoadedScalabilityTier = false;
            try
            {
                if (!TryReadPortableOptionsFile(path, out string json, out loadedScalabilityTier, out hasLoadedScalabilityTier))
                    json = ReadLegacyTextOptionsFile(path);

                ApplyOptionsJson(json);
                ApplyLoadedScalabilityTier(loadedScalabilityTier, hasLoadedScalabilityTier);
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[UserOptionsPersistence] Failed to read options.h8cfg: " + exception.Message);
#endif
                ApplyLoadedScalabilityTier(loadedScalabilityTier, hasLoadedScalabilityTier);
            }

            _loaded = true;
        }

        private void WritePortableOptionsFile(string path, string json)
        {
            int payloadLength = OptionsEncoding.GetByteCount(json);
            ValidatePayloadCapacity(payloadLength);
            if (payloadLength > 0)
                OptionsEncoding.GetBytes(json, 0, json.Length, _payloadBuffer, 0);

            WriteInt32LittleEndian(_headerBuffer, 0, FileMagic);
            WriteInt32LittleEndian(_headerBuffer, 4, FileVersion);
            WriteInt32LittleEndian(_headerBuffer, 8, payloadLength);
            _headerBuffer[12] = _scalabilityTier;
            _headerBuffer[13] = 0;
            _headerBuffer[14] = 0;
            _headerBuffer[15] = 0;

            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(_headerBuffer, 0, FileHeaderBytes);
                if (payloadLength > 0)
                    stream.Write(_payloadBuffer, 0, payloadLength);

                stream.SetLength(FixedOptionsFileBytes);
            }
        }

        private bool TryReadPortableOptionsFile(
            string path,
            out string json,
            out byte scalabilityTier,
            out bool hasScalabilityTier)
        {
            json = string.Empty;
            scalabilityTier = ResolveDefaultScalabilityTier();
            hasScalabilityTier = false;
            FileInfo fileInfo = new FileInfo(path);
            if (!fileInfo.Exists || fileInfo.Length < LegacyFileHeaderBytes)
                return false;

            long viewLength = fileInfo.Length > FixedOptionsFileBytes ? FixedOptionsFileBytes : fileInfo.Length;
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (!TryReadExact(stream, _headerBuffer, 0, LegacyFileHeaderBytes))
                    return false;

                int magic = ReadInt32LittleEndian(_headerBuffer, 0);
                if (magic != FileMagic)
                    return false;

                int version = ReadInt32LittleEndian(_headerBuffer, 4);
                int headerBytes = version >= FileVersion && viewLength >= FileHeaderBytes
                    ? FileHeaderBytes
                    : LegacyFileHeaderBytes;
                if (headerBytes == FileHeaderBytes)
                {
                    if (!TryReadExact(stream, _headerBuffer, LegacyFileHeaderBytes, FileHeaderBytes - LegacyFileHeaderBytes))
                        return false;

                    scalabilityTier = ScalabilityTierProfiles.Normalize(_headerBuffer[12]);
                    hasScalabilityTier = true;
                }

                int payloadLength = ReadInt32LittleEndian(_headerBuffer, 8);
                if (payloadLength < 0 ||
                    payloadLength > MaxOptionsPayloadBytes ||
                    payloadLength > viewLength - headerBytes)
                {
                    return false;
                }

                ValidatePayloadCapacity(payloadLength);
                if (payloadLength > 0)
                {
                    stream.Position = headerBytes;
                    if (!TryReadExact(stream, _payloadBuffer, 0, payloadLength))
                        return false;
                }

                json = OptionsEncoding.GetString(_payloadBuffer, 0, payloadLength);
                return true;
            }
        }

        private static void ValidatePayloadCapacity(int payloadLength)
        {
            if (payloadLength <= MaxOptionsPayloadBytes)
                return;

            throw new InvalidDataException("options.h8cfg payload exceeds fixed runtime buffer.");
        }

        private static string ReadLegacyTextOptionsFile(string path)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (StreamReader reader = new StreamReader(stream, OptionsEncoding, detectEncodingFromByteOrderMarks: true))
            {
                return reader.ReadToEnd();
            }
        }

        private static byte ResolveDefaultScalabilityTier()
        {
            return PlatformIntegrationBridge.ResolveCurrentScalabilityTier(DefaultScalabilityTier);
        }

        private void ApplyLoadedScalabilityTier(byte loadedTier, bool headerHasTier)
        {
            byte selectedTier = headerHasTier ? loadedTier : ResolveDefaultScalabilityTier();
            if (!headerHasTier && TryResolveScalabilityTierRecord(out byte recordTier))
                selectedTier = recordTier;

            _scalabilityTier = ScalabilityTierProfiles.Normalize(selectedTier);
            SyncScalabilityTierRecord();
            PlatformIntegrationBridge.ApplyScalabilityTier(_scalabilityTier);
        }

        private bool TryResolveScalabilityTierRecord(out byte tier)
        {
            if (_records.TryGetValue(ScalabilityTierKey, out OptionRecord record))
            {
                if (record.Type == TypeInt)
                {
                    tier = ScalabilityTierProfiles.Normalize((byte)record.IntValue);
                    return true;
                }

                if (record.Type == TypeBool)
                {
                    tier = record.BoolValue ? ScalabilityTierProfiles.HighRtx : ScalabilityTierProfiles.LowMx350;
                    return true;
                }
            }

            tier = DefaultScalabilityTier;
            return false;
        }

        private void SyncScalabilityTierRecord()
        {
            _records[ScalabilityTierKey] = new OptionRecord
            {
                Key = ScalabilityTierKey,
                Type = TypeInt,
                IntValue = _scalabilityTier
            };
        }

        private void ApplyOptionsJson(string json)
        {
            OptionsFile file = JsonUtility.FromJson<OptionsFile>(json);
            if (file?.Records == null)
                return;

            for (int i = 0; i < file.Records.Length; i++)
            {
                OptionRecord record = file.Records[i];
                if (string.IsNullOrWhiteSpace(record.Key))
                    continue;

                _records[record.Key] = record;
            }
        }

        private bool TryGetRecord(string key, out OptionRecord record)
        {
            EnsureLoaded();

            if (!string.IsNullOrWhiteSpace(key) && _records.TryGetValue(key, out record))
                return true;

            record = default;
            return false;
        }

        private string ResolveOptionsPath()
        {
            if (!string.IsNullOrEmpty(_optionsPath))
                return _optionsPath;

            _optionsDirectory = ResolvePersistentRootPath();
            _optionsPath = Path.Combine(_optionsDirectory, NormalizePersistentRelativeSegment(FileName));
            return _optionsPath;
        }

        private static string ResolvePersistentRootPath()
        {
            string root = Application.persistentDataPath;
            return string.IsNullOrEmpty(root) ? "." : root;
        }

        private static string NormalizePersistentRelativeSegment(string segment)
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

        private static bool TryReadExact(FileStream stream, byte[] buffer, int offset, int count)
        {
            int readBytes = 0;
            while (readBytes < count)
            {
                int justRead = stream.Read(buffer, offset + readBytes, count - readBytes);
                if (justRead <= 0)
                    return false;

                readBytes += justRead;
            }

            return true;
        }

        private static void WriteInt32LittleEndian(byte[] buffer, int offset, int value)
        {
            uint unsigned = unchecked((uint)value);
            buffer[offset] = (byte)unsigned;
            buffer[offset + 1] = (byte)(unsigned >> 8);
            buffer[offset + 2] = (byte)(unsigned >> 16);
            buffer[offset + 3] = (byte)(unsigned >> 24);
        }

        private static int ReadInt32LittleEndian(byte[] buffer, int offset)
        {
            return unchecked((int)(
                (uint)buffer[offset] |
                ((uint)buffer[offset + 1] << 8) |
                ((uint)buffer[offset + 2] << 16) |
                ((uint)buffer[offset + 3] << 24)));
        }

        [Serializable]
        private sealed class OptionsFile
        {
            public int Version;
            public OptionRecord[] Records = Array.Empty<OptionRecord>();
        }

        [Serializable]
        private struct OptionRecord
        {
            public string Key;
            public int Type;
            public int IntValue;
            public float FloatValue;
            public string StringValue;
            public bool BoolValue;
        }
    }
}
