using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
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
    public sealed class UserOptionsPersistence : MonoBehaviour, IServiceHeartbeat, IServiceShutdown
    {
        public const string LanguageKey = "Hecton_Language";
        public const string FileName = "options.h8cfg";

        private const int FileVersion = 1;
        private const int TypeInt = 1;
        private const int TypeFloat = 2;
        private const int TypeString = 3;
        private const int TypeBool = 4;
        private const int FileMagic = 0x46433848; // H8CF, little endian.
        private const int FileHeaderBytes = 12;
        private const int MaxOptionsPayloadBytes = 64 * 1024;
        private static readonly Encoding OptionsEncoding = new UTF8Encoding(false);

        private readonly Dictionary<string, OptionRecord> _records =
            new Dictionary<string, OptionRecord>(64); // COLD ALLOC: Dictionary<string, OptionRecord>[64] - user options key/value cache - owner: UserOptionsPersistence
        private readonly OptionsFile _optionsFile = new OptionsFile(); // COLD ALLOC: OptionsFile[1] - reusable MMF payload wrapper for options.h8cfg - owner: UserOptionsPersistence

        private OptionRecord[] _writeRecords = Array.Empty<OptionRecord>();
        private readonly byte[] _payloadBuffer = new byte[MaxOptionsPayloadBytes]; // COLD ALLOC: byte[64K] - fixed options.h8cfg MMF payload buffer - owner: UserOptionsPersistence
        private string _optionsPath;
        private string _optionsDirectory;
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

        public void Save()
        {
            EnsureLoaded();

            string path = ResolveOptionsPath();
            if (!string.IsNullOrEmpty(_optionsDirectory))
                Directory.CreateDirectory(_optionsDirectory);

            int recordCount = _records.Count;
            if (_writeRecords.Length != recordCount)
                _writeRecords = new OptionRecord[recordCount]; // COLD ALLOC: OptionRecord[count] - resized only when option key count changes - owner: UserOptionsPersistence

            _records.Values.CopyTo(_writeRecords, 0);
            _optionsFile.Version = FileVersion;
            _optionsFile.Records = _writeRecords;

            string tempPath = path + ".tmp";
            WriteMemoryMappedOptionsFile(tempPath, JsonUtility.ToJson(_optionsFile, false));

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
                _loaded = true;
                return;
            }

            try
            {
                if (!TryReadMemoryMappedOptionsFile(path, out string json))
                    json = ReadLegacyTextOptionsFile(path);

                ApplyOptionsJson(json);
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[UserOptionsPersistence] Failed to read options.h8cfg: " + exception.Message);
#endif
            }

            _loaded = true;
        }

        private void WriteMemoryMappedOptionsFile(string path, string json)
        {
            int payloadLength = OptionsEncoding.GetByteCount(json);
            ValidatePayloadCapacity(payloadLength);
            long fileLength = FileHeaderBytes + payloadLength;
            if (payloadLength > 0)
                OptionsEncoding.GetBytes(json, 0, json.Length, _payloadBuffer, 0);

            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                stream.SetLength(fileLength);
                using (MemoryMappedFile mappedFile = MemoryMappedFile.CreateFromFile(
                           stream,
                           null,
                           fileLength,
                           MemoryMappedFileAccess.ReadWrite,
                           HandleInheritability.None,
                           leaveOpen: true))
                using (MemoryMappedViewAccessor accessor = mappedFile.CreateViewAccessor(0L, fileLength, MemoryMappedFileAccess.Write))
                {
                    accessor.Write(0L, FileMagic);
                    accessor.Write(4L, FileVersion);
                    accessor.Write(8L, payloadLength);
                    if (payloadLength > 0)
                        accessor.WriteArray(FileHeaderBytes, _payloadBuffer, 0, payloadLength);
                }
            }
        }

        private bool TryReadMemoryMappedOptionsFile(string path, out string json)
        {
            json = string.Empty;
            FileInfo fileInfo = new FileInfo(path);
            if (!fileInfo.Exists || fileInfo.Length < FileHeaderBytes)
                return false;

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (MemoryMappedFile mappedFile = MemoryMappedFile.CreateFromFile(
                       stream,
                       null,
                       0L,
                       MemoryMappedFileAccess.Read,
                       HandleInheritability.None,
                       leaveOpen: false))
            using (MemoryMappedViewAccessor accessor = mappedFile.CreateViewAccessor(0L, fileInfo.Length, MemoryMappedFileAccess.Read))
            {
                int magic = accessor.ReadInt32(0L);
                if (magic != FileMagic)
                    return false;

                int payloadLength = accessor.ReadInt32(8L);
                if (payloadLength < 0 || payloadLength > fileInfo.Length - FileHeaderBytes)
                    return false;

                ValidatePayloadCapacity(payloadLength);
                if (payloadLength > 0)
                    accessor.ReadArray(FileHeaderBytes, _payloadBuffer, 0, payloadLength);

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

            _optionsDirectory = Application.persistentDataPath;
            _optionsPath = Path.Combine(_optionsDirectory, FileName);
            return _optionsPath;
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
