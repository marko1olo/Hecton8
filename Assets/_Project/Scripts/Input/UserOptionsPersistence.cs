using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Hecton8.Core;
using Hecton8.SaveSystem;
using Unity.Mathematics;
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
        private const string TempFileName = "options.h8cfg.tmp";

        private const int FileVersion = 3;
        private const int FileVersionWithScalabilityTier = 2;
        private const int TypeInt = 1;
        private const int TypeFloat = 2;
        private const int TypeString = 3;
        private const int TypeBool = 4;
        private const int FileMagic = 0x46433848; // H8CF, little endian.
        private const int BinaryPayloadMagic = 0x504F3848; // H8OP, little endian.
        private const int LegacyFileHeaderBytes = 12;
        private const int FileHeaderBytes = 16;
        private const int BinaryPayloadHeaderBytes = 8;
        private const int BinaryRecordHeaderBytes = 24;
        private const int MaxOptionsPayloadBytes = 64 * 1024;
        private const int MaxOptionRecords = 512;
        private const int MaxOptionKeyBytes = 256;
        private const int MaxOptionStringBytes = 4096;
        private const long FixedOptionsFileBytes = FileHeaderBytes + MaxOptionsPayloadBytes;
        private const byte DefaultScalabilityTier = ScalabilityTierProfiles.LowCompact;
        private static readonly Encoding OptionsEncoding = new UTF8Encoding(false, true);

        private readonly Dictionary<string, OptionRecord> _records =
            new Dictionary<string, OptionRecord>(64, StringComparer.Ordinal); // COLD ALLOC: Dictionary<string, OptionRecord>[64] - user options key/value cache - owner: UserOptionsPersistence

        private OptionRecord[] _writeRecords = Array.Empty<OptionRecord>();
        private readonly byte[] _payloadBuffer = new byte[MaxOptionsPayloadBytes]; // COLD ALLOC: byte[64K] - fixed options.h8cfg payload buffer - owner: UserOptionsPersistence
        private readonly byte[] _headerBuffer = new byte[FileHeaderBytes]; // COLD ALLOC: byte[16] - fixed options.h8cfg header buffer - owner: UserOptionsPersistence
        private string _optionsPath;
        private string _optionsTempPath;
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

        public string OptionsPath => _optionsPath ?? string.Empty;
        public bool LastSaveSucceeded { get; private set; }

        public byte ScalabilityTier
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _scalabilityTier;
        }

        private void Awake()
        {
            if (TryAbortForUsableExistingRuntime())
                return;

            EnsureOptionsStoragePaths();
            LoadFromDisk();
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
            if (!TrySave())
                Hecton8.Core.H8Debug.LogWarning("[UserOptionsPersistence] Failed to persist options.h8cfg during shutdown.");
            _serviceShutdownComplete = true;
        }

        private void RegisterService()
        {
            if (_serviceShuttingDown || !Application.isPlaying)
                return;

            if (TryAbortForUsableExistingRuntime())
                return;

            BootstrapRegistryBridge.TryResolve(BootstrapRegistryBridgeSlot.UserOptionsRuntime, out UserOptionsPersistence registered);
            if (!ReferenceEquals(registered, this))
                BootstrapRegistryBridge.Register(BootstrapRegistryBridgeSlot.UserOptionsRuntime, this);

            _serviceRegistered =
                BootstrapRegistryBridge.TryResolve(BootstrapRegistryBridgeSlot.UserOptionsRuntime, out registered) &&
                ReferenceEquals(registered, this);
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            BootstrapRegistryBridge.TryResolve(BootstrapRegistryBridgeSlot.UserOptionsRuntime, out UserOptionsPersistence registered);
            if (ReferenceEquals(registered, null) || ReferenceEquals(registered, this))
                return false;

            if (IsUserOptionsRuntimeUsable(registered))
            {
                DestroyDuplicateInstance();
                return true;
            }

            BootstrapRegistryBridge.Unregister(BootstrapRegistryBridgeSlot.UserOptionsRuntime, registered);
            return false;
        }

        private static bool IsUserOptionsRuntimeUsable(UserOptionsPersistence persistence)
        {
            return persistence != null &&
                   persistence._serviceRegistered &&
                   persistence.isActiveAndEnabled &&
                   !persistence._serviceShuttingDown;
        }

        private void DestroyDuplicateInstance()
        {
            _serviceShuttingDown = true;
            _serviceShutdownComplete = true;
            Destroy(gameObject);
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
            if (string.IsNullOrWhiteSpace(key))
                return false;

            EnsureLoaded();
            return _records.ContainsKey(key);
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
            if (!TrySaveToDisk())
            {
                LastSaveSucceeded = false;
                _scalabilityTier = previousTier;
                SyncScalabilityTierRecord();
                return;
            }

            LastSaveSucceeded = true;
            PlatformIntegrationBridge.ApplyScalabilityTier(normalizedTier);
            PlatformIntegrationBridge.PublishScalabilityChanged(previousTier, normalizedTier);
        }

        public void Save()
        {
            TrySave();
        }

        public bool TrySave()
        {
            EnsureLoaded();

            LastSaveSucceeded = TrySaveToDisk();
            return LastSaveSucceeded;
        }

        private bool TrySaveToDisk()
        {
            EnsureOptionsStoragePaths();
            string path = _optionsPath;
            string tempPath = _optionsTempPath;
            bool mayDeleteTemp = false;

            try
            {
                if (!TryResolveAtomicOptionsPaths(path, tempPath, out string absolutePath, out string absoluteTempPath))
                    return false;

                path = absolutePath;
                tempPath = absoluteTempPath;

                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                SyncScalabilityTierRecord();
                int recordCount = _records.Count;
                if (_writeRecords.Length != recordCount)
                    _writeRecords = new OptionRecord[recordCount]; // COLD ALLOC: OptionRecord[count] - resized only when option key count changes - owner: UserOptionsPersistence

                _records.Values.CopyTo(_writeRecords, 0);

                mayDeleteTemp = true;
                if (!WritePortableOptionsFile(tempPath, _writeRecords, recordCount))
                    return false;

                ReplaceOptionsFile(tempPath, path);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (IOException)
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
            catch (EncoderFallbackException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
            finally
            {
                if (mayDeleteTemp)
                    DeleteOptionsTempBestEffort(tempPath);
            }
        }

        private static bool TryResolveAtomicOptionsPaths(string path, string tempPath, out string absolutePath, out string absoluteTempPath)
        {
            absolutePath = null;
            absoluteTempPath = null;

            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(tempPath))
                return false;

            absolutePath = Path.GetFullPath(path);
            absoluteTempPath = Path.GetFullPath(tempPath);

            if (AreSameFullPath(absolutePath, absoluteTempPath))
                return false;

            string directory = Path.GetDirectoryName(absolutePath);
            string tempDirectory = Path.GetDirectoryName(absoluteTempPath);
            if (!AreSameFullPath(directory ?? string.Empty, tempDirectory ?? string.Empty))
                return false;

            return true;
        }

        private static bool AreSameFullPath(string left, string right)
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
#else
            return string.Equals(left, right, StringComparison.Ordinal);
#endif
        }

        private static void ReplaceOptionsFile(string tempPath, string path)
        {
            if (!AsyncWriteManager.TryGetFileLength(tempPath, out long tempOptionsBytes, out string tempLengthError))
                throw new IOException(string.IsNullOrEmpty(tempLengthError) ? "Options temp file length could not be resolved before promotion." : tempLengthError);

            if (tempOptionsBytes != FixedOptionsFileBytes)
                throw new IOException("Options temp file length changed before promotion.");

            if (!AsyncWriteManager.FlushCriticalSavePath(tempPath, tempOptionsBytes, out string tempFlushError))
                throw new IOException(string.IsNullOrEmpty(tempFlushError) ? "Options temp critical flush failed before promotion." : tempFlushError);

            AsyncWriteManager.InvalidateCachedReadWindows(tempPath);
            AsyncWriteManager.InvalidateCachedReadWindows(path);
            try
            {
                if (File.Exists(path))
                {
                    File.Replace(tempPath, path, null, ignoreMetadataErrors: true);
                }
                else
                {
                    File.Move(tempPath, path);
                }
            }
            finally
            {
                AsyncWriteManager.InvalidateCachedReadWindows(tempPath);
                AsyncWriteManager.InvalidateCachedReadWindows(path);
            }

            if (!AsyncWriteManager.TryGetFileLength(path, out long promotedOptionsBytes, out string lengthError))
                throw new IOException(string.IsNullOrEmpty(lengthError) ? "Options file length could not be resolved after promotion." : lengthError);

            if (promotedOptionsBytes != FixedOptionsFileBytes)
                throw new IOException("Options file length changed during promotion.");

            if (!AsyncWriteManager.FlushCriticalSavePath(path, promotedOptionsBytes, out string flushError))
                throw new IOException(string.IsNullOrEmpty(flushError) ? "Options critical flush failed after promotion." : flushError);
        }

        private static void DeleteOptionsTempBestEffort(string tempPath)
        {
            if (string.IsNullOrEmpty(tempPath))
                return;

            AsyncWriteManager.InvalidateCachedReadWindows(tempPath);
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (IOException)
            {
            }
            catch (NotSupportedException)
            {
            }
            catch (ArgumentException)
            {
            }
            catch (System.Security.SecurityException)
            {
            }
            finally
            {
                AsyncWriteManager.InvalidateCachedReadWindows(tempPath);
            }
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
            EnsureOptionsStoragePaths();
            string path = _optionsPath;
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
                bool loadedPortableOptions = TryReadPortableOptionsFile(path, out loadedScalabilityTier, out hasLoadedScalabilityTier, out bool wasPortableContainer);
                if (!loadedPortableOptions)
                {
                    if (wasPortableContainer ||
                        !TryApplyLegacyOptionsJson(ReadLegacyTextOptionsFile(path)))
                    {
                        LogRejectedOptionsFile();
                    }
                }

                ApplyLoadedScalabilityTier(loadedScalabilityTier, hasLoadedScalabilityTier);
            }
            catch (UnauthorizedAccessException)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError("[UserOptionsPersistence] Failed to read options.h8cfg.");
#endif
                ApplyLoadedScalabilityTier(loadedScalabilityTier, hasLoadedScalabilityTier);
            }
            catch (IOException)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError("[UserOptionsPersistence] Failed to read options.h8cfg.");
#endif
                ApplyLoadedScalabilityTier(loadedScalabilityTier, hasLoadedScalabilityTier);
            }
            catch (NotSupportedException)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError("[UserOptionsPersistence] Failed to read options.h8cfg.");
#endif
                ApplyLoadedScalabilityTier(loadedScalabilityTier, hasLoadedScalabilityTier);
            }
            catch (DecoderFallbackException)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError("[UserOptionsPersistence] Failed to decode options.h8cfg.");
#endif
                ApplyLoadedScalabilityTier(loadedScalabilityTier, hasLoadedScalabilityTier);
            }
            catch (ArgumentException)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError("[UserOptionsPersistence] Failed to read options.h8cfg.");
#endif
                ApplyLoadedScalabilityTier(loadedScalabilityTier, hasLoadedScalabilityTier);
            }

            _loaded = true;
        }

        private static void LogRejectedOptionsFile()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogWarning("[UserOptionsPersistence] Rejected invalid options.h8cfg.");
#endif
        }

        private bool WritePortableOptionsFile(string path, OptionRecord[] records, int recordCount)
        {
            int payloadLength = TryWriteBinaryOptionsPayload(records, recordCount, _payloadBuffer, MaxOptionsPayloadBytes);
            if (!IsPayloadWithinCapacity(payloadLength))
                return false;

            WriteInt32LittleEndian(_headerBuffer, 0, FileMagic);
            WriteInt32LittleEndian(_headerBuffer, 4, FileVersion);
            WriteInt32LittleEndian(_headerBuffer, 8, payloadLength);
            _headerBuffer[12] = _scalabilityTier;
            _headerBuffer[13] = 0;
            _headerBuffer[14] = 0;
            _headerBuffer[15] = 0;

            AsyncWriteManager.InvalidateCachedReadWindows(path);
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    stream.Write(_headerBuffer, 0, FileHeaderBytes);
                    if (payloadLength > 0)
                        stream.Write(_payloadBuffer, 0, payloadLength);

                    stream.SetLength(FixedOptionsFileBytes);
                    stream.Flush(true);
                }
            }
            finally
            {
                AsyncWriteManager.InvalidateCachedReadWindows(path);
            }

            return true;
        }

        private bool TryReadPortableOptionsFile(
            string path,
            out byte scalabilityTier,
            out bool hasScalabilityTier,
            out bool wasPortableContainer)
        {
            scalabilityTier = ResolveDefaultScalabilityTier();
            hasScalabilityTier = false;
            wasPortableContainer = false;

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                long fileLength = stream.Length;
                if (fileLength < LegacyFileHeaderBytes)
                    return false;

                if (fileLength > FixedOptionsFileBytes)
                    return false;

                long viewLength = fileLength;
                if (!TryReadExact(stream, _headerBuffer, 0, LegacyFileHeaderBytes))
                    return false;

                int magic = ReadInt32LittleEndian(_headerBuffer, 0);
                if (magic != FileMagic)
                    return false;

                wasPortableContainer = true;
                int version = ReadInt32LittleEndian(_headerBuffer, 4);
                if (version <= 0 || version > FileVersion)
                    return false;

                int headerBytes = version >= FileVersionWithScalabilityTier && viewLength >= FileHeaderBytes
                    ? FileHeaderBytes
                    : LegacyFileHeaderBytes;
                byte headerTier = ResolveDefaultScalabilityTier();
                bool headerHasTier = false;
                if (headerBytes == FileHeaderBytes)
                {
                    if (!TryReadExact(stream, _headerBuffer, LegacyFileHeaderBytes, FileHeaderBytes - LegacyFileHeaderBytes))
                        return false;

                    headerTier = ScalabilityTierProfiles.Normalize(_headerBuffer[12]);
                    headerHasTier = true;
                }

                int payloadLength = ReadInt32LittleEndian(_headerBuffer, 8);
                if (payloadLength < 0 ||
                    payloadLength > MaxOptionsPayloadBytes ||
                    payloadLength > viewLength - headerBytes)
                {
                    return false;
                }

                if (!IsPayloadWithinCapacity(payloadLength))
                    return false;

                if (payloadLength > 0)
                {
                    stream.Position = headerBytes;
                    if (!TryReadExact(stream, _payloadBuffer, 0, payloadLength))
                        return false;
                }

                bool payloadApplied = version >= FileVersion
                    ? TryApplyBinaryOptionsPayload(_payloadBuffer, payloadLength)
                    : TryApplyLegacyOptionsJson(OptionsEncoding.GetString(_payloadBuffer, 0, payloadLength));

                if (!payloadApplied)
                    return false;

                scalabilityTier = headerTier;
                hasScalabilityTier = headerHasTier;
                return true;
            }
        }

        private static int TryWriteBinaryOptionsPayload(
            OptionRecord[] records,
            int recordCount,
            byte[] buffer,
            int capacity)
        {
            if (records == null || recordCount < 0 || recordCount > records.Length || recordCount > MaxOptionRecords)
                return -1;

            int index = 0;
            if (!TryWriteInt32(buffer, capacity, ref index, BinaryPayloadMagic))
                return -1;

            if (!TryWriteInt32(buffer, capacity, ref index, 0))
                return -1;

            int writtenRecords = 0;
            for (int i = 0; i < recordCount; i++)
            {
                OptionRecord record = records[i];
                if (string.IsNullOrWhiteSpace(record.Key) || !IsSupportedOptionType(record.Type))
                    continue;

                string stringValue = record.Type == TypeString ? record.StringValue ?? string.Empty : string.Empty;
                int keyBytes = OptionsEncoding.GetByteCount(record.Key);
                int stringBytes = record.Type == TypeString ? OptionsEncoding.GetByteCount(stringValue) : 0;
                if (keyBytes <= 0 ||
                    keyBytes > MaxOptionKeyBytes ||
                    stringBytes > MaxOptionStringBytes ||
                    index + BinaryRecordHeaderBytes + keyBytes + stringBytes > capacity)
                {
                    return -1;
                }

                if (!TryWriteInt32(buffer, capacity, ref index, record.Type))
                    return -1;

                if (!TryWriteInt32(buffer, capacity, ref index, record.IntValue))
                    return -1;

                if (!TryWriteInt32(buffer, capacity, ref index, unchecked((int)math.asuint(record.FloatValue))))
                    return -1;

                if (index + 4 > capacity)
                    return -1;

                buffer[index++] = record.BoolValue ? (byte)1 : (byte)0;
                buffer[index++] = 0;
                buffer[index++] = 0;
                buffer[index++] = 0;

                if (!TryWriteInt32(buffer, capacity, ref index, keyBytes))
                    return -1;

                if (!TryWriteInt32(buffer, capacity, ref index, stringBytes))
                    return -1;

                OptionsEncoding.GetBytes(record.Key, 0, record.Key.Length, buffer, index);
                index += keyBytes;

                if (stringBytes > 0)
                {
                    OptionsEncoding.GetBytes(stringValue, 0, stringValue.Length, buffer, index);
                    index += stringBytes;
                }

                writtenRecords++;
            }

            WriteInt32LittleEndian(buffer, 4, writtenRecords);
            return index;
        }

        private bool TryApplyBinaryOptionsPayload(byte[] buffer, int payloadLength)
        {
            if (buffer == null || payloadLength < BinaryPayloadHeaderBytes)
                return false;

            int magic = ReadInt32LittleEndian(buffer, 0);
            if (magic != BinaryPayloadMagic)
                return false;

            int recordCount = ReadInt32LittleEndian(buffer, 4);
            if (recordCount < 0 || recordCount > MaxOptionRecords)
                return false;

            if (!EnsureWriteRecordCapacity(recordCount))
                return false;

            int index = BinaryPayloadHeaderBytes;
            int stagedRecords = 0;
            for (int i = 0; i < recordCount; i++)
            {
                if (index + BinaryRecordHeaderBytes > payloadLength)
                    return false;

                int type = ReadInt32LittleEndian(buffer, index);
                int intValue = ReadInt32LittleEndian(buffer, index + 4);
                uint floatBits = unchecked((uint)ReadInt32LittleEndian(buffer, index + 8));
                bool boolValue = buffer[index + 12] != 0;
                int keyBytes = ReadInt32LittleEndian(buffer, index + 16);
                int stringBytes = ReadInt32LittleEndian(buffer, index + 20);
                index += BinaryRecordHeaderBytes;

                if (!IsSupportedOptionType(type) ||
                    keyBytes <= 0 ||
                    keyBytes > MaxOptionKeyBytes ||
                    stringBytes < 0 ||
                    stringBytes > MaxOptionStringBytes ||
                    index + keyBytes + stringBytes > payloadLength)
                {
                    return false;
                }

                string key = OptionsEncoding.GetString(buffer, index, keyBytes);
                index += keyBytes;
                string stringValue = string.Empty;
                if (stringBytes > 0)
                {
                    stringValue = OptionsEncoding.GetString(buffer, index, stringBytes);
                    index += stringBytes;
                }

                if (string.IsNullOrWhiteSpace(key))
                    return false;

                _writeRecords[stagedRecords++] = new OptionRecord
                {
                    Key = key,
                    Type = type,
                    IntValue = intValue,
                    FloatValue = math.asfloat(floatBits),
                    StringValue = stringValue,
                    BoolValue = boolValue
                };
            }

            if (index != payloadLength)
                return false;

            ApplyStagedOptionRecords(stagedRecords);
            return true;
        }

        private bool EnsureWriteRecordCapacity(int recordCount)
        {
            if (recordCount < 0 || recordCount > MaxOptionRecords)
                return false;

            if (_writeRecords.Length < recordCount)
            {
                int newCapacity = _writeRecords.Length <= 0 ? 4 : _writeRecords.Length;
                while (newCapacity < recordCount && newCapacity < MaxOptionRecords)
                    newCapacity <<= 1;

                if (newCapacity > MaxOptionRecords)
                    newCapacity = MaxOptionRecords;

                _writeRecords = new OptionRecord[newCapacity]; // COLD ALLOC: OptionRecord[capacity] - staged options load/apply buffer - owner: UserOptionsPersistence
            }

            return true;
        }

        private void ApplyStagedOptionRecords(int recordCount)
        {
            for (int i = 0; i < recordCount; i++)
            {
                OptionRecord record = _writeRecords[i];
                if (string.IsNullOrWhiteSpace(record.Key))
                    continue;

                _records[record.Key] = record;
            }
        }

        private static bool IsPayloadWithinCapacity(int payloadLength)
        {
            return payloadLength >= 0 && payloadLength <= MaxOptionsPayloadBytes;
        }

        private static bool IsSupportedOptionType(int type)
        {
            return type == TypeInt ||
                   type == TypeFloat ||
                   type == TypeString ||
                   type == TypeBool;
        }

        private static bool TryWriteInt32(byte[] buffer, int capacity, ref int index, int value)
        {
            if (buffer == null || index < 0 || index + 4 > capacity || index + 4 > buffer.Length)
                return false;

            WriteInt32LittleEndian(buffer, index, value);
            index += 4;
            return true;
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
                    tier = record.BoolValue ? ScalabilityTierProfiles.HighDiscrete : ScalabilityTierProfiles.LowCompact;
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

        private bool TryApplyLegacyOptionsJson(string json)
        {
            if (string.IsNullOrEmpty(json))
                return false;

            if (!TryGetLegacyRecordsArrayRange(json, out int arrayStart, out int arrayEnd))
                return false;

            return TryParseAndApplyLegacyRecordsArray(json, arrayStart, arrayEnd);
        }

        private static bool TryGetLegacyRecordsArrayRange(string json, out int arrayStart, out int arrayEnd)
        {
            arrayStart = 0;
            arrayEnd = 0;

            int rootObjectStart = 0;
            SkipJsonWhitespace(json, ref rootObjectStart, json.Length);
            if (rootObjectStart >= json.Length || json[rootObjectStart] != '{')
                return false;

            if (!TryFindJsonObjectEnd(json, rootObjectStart, out int rootObjectEnd))
                return false;

            int tail = rootObjectEnd + 1;
            SkipJsonWhitespace(json, ref tail, json.Length);
            if (tail != json.Length)
                return false;

            if (!TryFindTopLevelJsonPropertyRange(
                    json,
                    rootObjectStart,
                    rootObjectEnd,
                    "Records",
                    out arrayStart,
                    out arrayEnd))
                return false;

            if (arrayStart >= arrayEnd || json[arrayStart] != '[')
                return false;

            return true;
        }

        private bool TryParseAndApplyLegacyRecordsArray(string json, int arrayStart, int arrayEnd)
        {
            int index = arrayStart + 1;
            int stagedRecords = 0;
            while (index < arrayEnd)
            {
                SkipJsonWhitespace(json, ref index, arrayEnd);
                if (index >= arrayEnd)
                    return false;

                char token = json[index];
                if (token == ']')
                {
                    int afterArray = index + 1;
                    SkipJsonWhitespace(json, ref afterArray, arrayEnd);
                    if (afterArray != arrayEnd)
                        return false;

                    ApplyStagedOptionRecords(stagedRecords);
                    return true;
                }

                if (token == ',')
                {
                    index++;
                    continue;
                }

                if (token != '{')
                    return false;

                int recordObjectStart = index;
                if (!TryFindJsonObjectEnd(json, recordObjectStart, arrayEnd, out int recordObjectEnd))
                    return false;

                if (!TryReadLegacyOptionRecord(json, recordObjectStart, recordObjectEnd, out OptionRecord record))
                    return false;

                if (!EnsureWriteRecordCapacity(stagedRecords + 1))
                    return false;

                _writeRecords[stagedRecords++] = record;

                index = recordObjectEnd + 1;
            }

            return false;
        }

        private static bool TryReadLegacyOptionRecord(
            string json,
            int objectStart,
            int objectEnd,
            out OptionRecord record)
        {
            record = default;
            if (!TryReadTopLevelJsonStringProperty(json, objectStart, objectEnd, "Key", out string key) ||
                string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            if (!TryReadTopLevelJsonIntProperty(json, objectStart, objectEnd, "Type", out int type) ||
                !IsSupportedOptionType(type))
            {
                return false;
            }

            if (!TryReadOptionalTopLevelJsonIntProperty(json, objectStart, objectEnd, "IntValue", out int intValue))
                return false;

            if (!TryReadOptionalTopLevelJsonFloatProperty(json, objectStart, objectEnd, "FloatValue", out float floatValue))
                return false;

            if (!TryReadOptionalTopLevelJsonStringProperty(json, objectStart, objectEnd, "StringValue", out string stringValue))
                return false;

            if (!TryReadOptionalTopLevelJsonBoolProperty(json, objectStart, objectEnd, "BoolValue", out bool boolValue))
                return false;

            record = new OptionRecord
            {
                Key = key,
                Type = type,
                IntValue = intValue,
                FloatValue = floatValue,
                StringValue = stringValue ?? string.Empty,
                BoolValue = boolValue
            };
            return true;
        }

        private static bool TryReadTopLevelJsonStringProperty(
            string json,
            int objectStart,
            int objectEnd,
            string propertyName,
            out string value)
        {
            value = string.Empty;
            return TryFindTopLevelJsonPropertyRange(json, objectStart, objectEnd, propertyName, out int valueStart, out int valueEnd) &&
                   TryReadJsonStringValue(json, valueStart, valueEnd, out value);
        }

        private static bool TryReadOptionalTopLevelJsonStringProperty(
            string json,
            int objectStart,
            int objectEnd,
            string propertyName,
            out string value)
        {
            value = string.Empty;
            if (!TryFindTopLevelJsonPropertyRange(json, objectStart, objectEnd, propertyName, out int valueStart, out int valueEnd, out bool found))
                return false;

            if (!found)
                return true;

            return TryReadJsonStringValue(json, valueStart, valueEnd, out value);
        }

        private static bool TryReadTopLevelJsonIntProperty(
            string json,
            int objectStart,
            int objectEnd,
            string propertyName,
            out int value)
        {
            value = 0;
            return TryFindTopLevelJsonPropertyRange(json, objectStart, objectEnd, propertyName, out int valueStart, out int valueEnd) &&
                   TryReadJsonIntValue(json, valueStart, valueEnd, out value);
        }

        private static bool TryReadOptionalTopLevelJsonIntProperty(
            string json,
            int objectStart,
            int objectEnd,
            string propertyName,
            out int value)
        {
            value = 0;
            if (!TryFindTopLevelJsonPropertyRange(json, objectStart, objectEnd, propertyName, out int valueStart, out int valueEnd, out bool found))
                return false;

            if (!found)
                return true;

            return TryReadJsonIntValue(json, valueStart, valueEnd, out value);
        }

        private static bool TryReadOptionalTopLevelJsonFloatProperty(
            string json,
            int objectStart,
            int objectEnd,
            string propertyName,
            out float value)
        {
            value = 0f;
            if (!TryFindTopLevelJsonPropertyRange(json, objectStart, objectEnd, propertyName, out int valueStart, out int valueEnd, out bool found))
                return false;

            if (!found)
                return true;

            return TryReadJsonFloatValue(json, valueStart, valueEnd, out value);
        }

        private static bool TryReadOptionalTopLevelJsonBoolProperty(
            string json,
            int objectStart,
            int objectEnd,
            string propertyName,
            out bool value)
        {
            value = false;
            if (!TryFindTopLevelJsonPropertyRange(json, objectStart, objectEnd, propertyName, out int valueStart, out int valueEnd, out bool found))
                return false;

            if (!found)
                return true;

            return TryReadJsonBoolValue(json, valueStart, valueEnd, out value);
        }

        private static bool TryReadJsonStringValue(string json, int valueStart, int end, out string value)
        {
            value = string.Empty;
            SkipJsonWhitespace(json, ref valueStart, end);
            if (valueStart >= end || json[valueStart] != '"')
                return false;

            if (!TrySkipJsonString(json, valueStart, end, out int stringEnd))
                return false;

            int tail = stringEnd + 1;
            SkipJsonWhitespace(json, ref tail, end);
            return tail == end && TryReadJsonString(json, valueStart, end, out value);
        }

        private static bool TryReadJsonIntValue(string json, int valueStart, int end, out int value)
        {
            value = 0;
            SkipJsonWhitespace(json, ref valueStart, end);
            bool negative = false;
            if (valueStart < end && json[valueStart] == '-')
            {
                negative = true;
                valueStart++;
            }

            long parsed = 0L;
            int digits = 0;
            while (valueStart < end)
            {
                char c = json[valueStart];
                if (c < '0' || c > '9')
                    break;

                int digit = c - '0';
                if (parsed > (long.MaxValue - digit) / 10L)
                    return false;

                parsed = parsed * 10L + digit;
                if ((!negative && parsed > int.MaxValue) ||
                    (negative && -parsed < int.MinValue))
                {
                    return false;
                }

                digits++;
                valueStart++;
            }

            if (digits == 0)
                return false;

            SkipJsonWhitespace(json, ref valueStart, end);
            if (valueStart != end)
                return false;

            value = negative ? (int)-parsed : (int)parsed;
            return true;
        }

        private static bool TryReadJsonFloatValue(string json, int valueStart, int end, out float value)
        {
            value = 0f;
            SkipJsonWhitespace(json, ref valueStart, end);
            int tokenStart = valueStart;
            while (valueStart < end)
            {
                char c = json[valueStart];
                if (char.IsWhiteSpace(c))
                    break;

                valueStart++;
            }

            if (valueStart <= tokenStart)
                return false;

            int tail = valueStart;
            SkipJsonWhitespace(json, ref tail, end);
            if (tail != end)
                return false;

            ReadOnlySpan<char> token = json.AsSpan(tokenStart, valueStart - tokenStart);
            return float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryReadJsonBoolValue(string json, int valueStart, int end, out bool value)
        {
            value = false;
            SkipJsonWhitespace(json, ref valueStart, end);
            if (MatchesJsonLiteral(json, valueStart, end, "true"))
            {
                int tail = valueStart + 4;
                SkipJsonWhitespace(json, ref tail, end);
                if (tail != end)
                    return false;

                value = true;
                return true;
            }

            if (MatchesJsonLiteral(json, valueStart, end, "false"))
            {
                int tail = valueStart + 5;
                SkipJsonWhitespace(json, ref tail, end);
                return tail == end;
            }

            return false;
        }

        private static bool TryFindTopLevelJsonPropertyRange(
            string json,
            int objectStart,
            int objectEnd,
            string propertyName,
            out int valueStart,
            out int valueEnd)
        {
            return TryFindTopLevelJsonPropertyRange(
                json,
                objectStart,
                objectEnd,
                propertyName,
                out valueStart,
                out valueEnd,
                out bool found) && found;
        }

        private static bool TryFindTopLevelJsonPropertyRange(
            string json,
            int objectStart,
            int objectEnd,
            string propertyName,
            out int valueStart,
            out int valueEnd,
            out bool found)
        {
            valueStart = 0;
            valueEnd = 0;
            found = false;
            if (string.IsNullOrEmpty(json) ||
                string.IsNullOrEmpty(propertyName) ||
                objectStart < 0 ||
                objectEnd <= objectStart ||
                objectEnd >= json.Length ||
                json[objectStart] != '{' ||
                json[objectEnd] != '}')
            {
                return false;
            }

            int cursor = objectStart + 1;
            while (cursor < objectEnd)
            {
                SkipJsonWhitespace(json, ref cursor, objectEnd);
                if (cursor == objectEnd)
                    return true;

                if (json[cursor] != '"')
                    return false;

                bool propertyMatches = TryMatchJsonStringLiteral(json, cursor, objectEnd, propertyName, out _);
                if (!TrySkipJsonString(json, cursor, objectEnd, out int nameEnd))
                    return false;

                cursor = nameEnd + 1;
                SkipJsonWhitespace(json, ref cursor, objectEnd);
                if (cursor >= objectEnd || json[cursor] != ':')
                    return false;

                cursor++;
                SkipJsonWhitespace(json, ref cursor, objectEnd);
                int candidateValueStart = cursor;
                if (!TrySkipJsonValue(json, ref cursor, objectEnd))
                    return false;

                if (propertyMatches)
                {
                    if (found)
                        return false;

                    found = true;
                    valueStart = candidateValueStart;
                    valueEnd = cursor;
                }

                SkipJsonWhitespace(json, ref cursor, objectEnd);
                if (cursor == objectEnd)
                    return true;

                if (json[cursor] != ',')
                    return false;

                cursor++;
            }

            return true;
        }

        private static bool TryMatchJsonStringLiteral(
            string json,
            int quoteIndex,
            int end,
            string expected,
            out int afterQuote)
        {
            afterQuote = 0;
            int cursor = quoteIndex + 1;
            for (int i = 0; i < expected.Length; i++)
            {
                if (cursor >= end || json[cursor] != expected[i])
                    return false;

                cursor++;
            }

            if (cursor >= end || json[cursor] != '"')
                return false;

            afterQuote = cursor + 1;
            return true;
        }

        private static bool TrySkipJsonValue(string json, ref int index, int end)
        {
            if (string.IsNullOrEmpty(json) || index < 0 || index >= end || end > json.Length)
                return false;

            char first = json[index];
            if (first == '"')
            {
                if (!TrySkipJsonString(json, index, end, out int stringEnd))
                    return false;

                index = stringEnd + 1;
                return true;
            }

            if (first == '{' || first == '[')
            {
                int depth = 0;
                bool inString = false;
                bool escaped = false;
                for (int i = index; i < end; i++)
                {
                    char c = json[i];
                    if (inString)
                    {
                        if (escaped)
                        {
                            escaped = false;
                            continue;
                        }

                        if (c == '\\')
                        {
                            escaped = true;
                            continue;
                        }

                        if (c == '"')
                            inString = false;

                        continue;
                    }

                    if (c == '"')
                    {
                        inString = true;
                        continue;
                    }

                    if (c == '{' || c == '[')
                    {
                        depth++;
                        continue;
                    }

                    if (c == '}' || c == ']')
                    {
                        depth--;
                        if (depth == 0)
                        {
                            index = i + 1;
                            return true;
                        }

                        if (depth < 0)
                            return false;
                    }
                }

                return false;
            }

            int tokenStart = index;
            while (index < end)
            {
                char c = json[index];
                if (c == ',' || c == '}' || c == ']' || char.IsWhiteSpace(c))
                    break;

                index++;
            }

            return index > tokenStart;
        }

        private static bool TryFindJsonObjectEnd(string json, int objectStart, out int objectEnd)
        {
            return TryFindJsonObjectEnd(json, objectStart, json.Length, out objectEnd);
        }

        private static bool TryFindJsonObjectEnd(string json, int objectStart, int end, out int objectEnd)
        {
            objectEnd = objectStart;
            if (string.IsNullOrEmpty(json) || objectStart < 0 || objectStart >= end || end > json.Length)
                return false;

            int depth = 0;
            bool inString = false;
            bool escaped = false;
            for (int i = objectStart; i < end; i++)
            {
                char c = json[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }

                    if (c == '\\')
                    {
                        escaped = true;
                        continue;
                    }

                    if (c == '"')
                        inString = false;

                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    continue;
                }

                if (c == '{')
                {
                    depth++;
                    continue;
                }

                if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        objectEnd = i;
                        return true;
                    }

                    if (depth < 0)
                        return false;
                }
            }

            return false;
        }

        private static bool TryReadJsonString(string json, int quoteIndex, int end, out string value)
        {
            value = string.Empty;
            if (quoteIndex >= end || json[quoteIndex] != '"')
                return false;

            for (int i = quoteIndex + 1; i < end; i++)
            {
                char c = json[i];
                if (c == '"')
                {
                    value = json.Substring(quoteIndex + 1, i - quoteIndex - 1);
                    return true;
                }

                if (c == '\\')
                    return TryDecodeEscapedJsonString(json, quoteIndex, end, out value);
            }

            return false;
        }

        private static bool TryDecodeEscapedJsonString(string json, int quoteIndex, int end, out string value)
        {
            value = string.Empty;
            char[] chars = new char[end - quoteIndex]; // COLD ALLOC: legacy options string decode only.
            int written = 0;
            for (int i = quoteIndex + 1; i < end; i++)
            {
                char c = json[i];
                if (c == '"')
                {
                    value = new string(chars, 0, written); // COLD ALLOC: legacy options.h8cfg migration only.
                    return true;
                }

                if (c != '\\')
                {
                    chars[written++] = c;
                    continue;
                }

                i++;
                if (i >= end)
                    return false;

                char escape = json[i];
                if (escape == '"' || escape == '\\' || escape == '/')
                    chars[written++] = escape;
                else if (escape == 'b')
                    chars[written++] = '\b';
                else if (escape == 'f')
                    chars[written++] = '\f';
                else if (escape == 'n')
                    chars[written++] = '\n';
                else if (escape == 'r')
                    chars[written++] = '\r';
                else if (escape == 't')
                    chars[written++] = '\t';
                else if (escape == 'u')
                {
                    if (i + 4 >= end || !TryReadJsonHex4(json, i + 1, out int codepoint))
                        return false;

                    chars[written++] = (char)codepoint;
                    i += 4;
                }
                else
                {
                    return false;
                }
            }

            return false;
        }

        private static bool TrySkipJsonString(string json, int quoteIndex, int end, out int stringEnd)
        {
            stringEnd = quoteIndex;
            bool escaped = false;
            for (int i = quoteIndex + 1; i < end; i++)
            {
                char c = json[i];
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (c == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (c == '"')
                {
                    stringEnd = i;
                    return true;
                }
            }

            return false;
        }

        private static bool TryReadJsonHex4(string json, int start, out int value)
        {
            value = 0;
            if (start < 0 || start + 4 > json.Length)
                return false;

            for (int i = 0; i < 4; i++)
            {
                char c = json[start + i];
                int digit;
                if (c >= '0' && c <= '9')
                    digit = c - '0';
                else if (c >= 'a' && c <= 'f')
                    digit = c - 'a' + 10;
                else if (c >= 'A' && c <= 'F')
                    digit = c - 'A' + 10;
                else
                    return false;

                value = (value << 4) | digit;
            }

            return true;
        }

        private static bool MatchesJsonLiteral(string json, int start, int end, string literal)
        {
            if (start < 0 || start + literal.Length > end)
                return false;

            for (int i = 0; i < literal.Length; i++)
            {
                if (json[start + i] != literal[i])
                    return false;
            }

            return true;
        }

        private static void SkipJsonWhitespace(string json, ref int index, int end)
        {
            while (index < end && char.IsWhiteSpace(json[index]))
                index++;
        }

        private bool TryGetRecord(string key, out OptionRecord record)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                record = default;
                return false;
            }

            EnsureLoaded();
            if (_records.TryGetValue(key, out record))
                return true;

            record = default;
            return false;
        }

        private void EnsureOptionsStoragePaths()
        {
            if (string.IsNullOrEmpty(_optionsDirectory))
                _optionsDirectory = ResolvePersistentRootPath();

            if (string.IsNullOrEmpty(_optionsPath))
                _optionsPath = Path.Combine(_optionsDirectory, NormalizePersistentRelativeSegment(FileName));

            if (string.IsNullOrEmpty(_optionsTempPath))
                _optionsTempPath = Path.Combine(_optionsDirectory, NormalizePersistentRelativeSegment(TempFileName));
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

            if (!ContainsPathSeparator(segment) &&
                segment.IndexOf("..", StringComparison.Ordinal) < 0)
            {
                return segment;
            }

            string fileName = Path.GetFileName(segment);
            return string.IsNullOrEmpty(fileName) ||
                   fileName.IndexOf("..", StringComparison.Ordinal) >= 0
                ? string.Empty
                : fileName;
        }

        private static bool ContainsPathSeparator(string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '\\' || c == '/' || c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar)
                    return true;
            }

            return false;
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
