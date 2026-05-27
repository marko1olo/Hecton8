using System.Collections.Generic;
using System.IO;
using System.Text;
using Hecton.Localization;
using UnityEngine;

namespace Hecton8.Modding
{
    /// <summary>
    /// Legacy owner for mod content assets loaded from on-disk AssetBundles and selected raw file fallbacks.
    /// Envelope-only UGC mode disables registration and loading; active assets must pass the FutureCommandEnvelope CRC gate.
    /// </summary>
    internal static class ModAssetManager
    {
        // COLD ALLOC: Dictionary<uint,string>[32] - mod hash to bundle path lookup - owner: ModAssetManager
        private static readonly Dictionary<uint, string> _bundlePaths = new Dictionary<uint, string>(32);
        // COLD ALLOC: Dictionary<uint,AssetBundle>[32] - cached loaded mod bundles by mod hash - owner: ModAssetManager
        private static readonly Dictionary<uint, AssetBundle> _loadedBundles = new Dictionary<uint, AssetBundle>(32);
        // COLD ALLOC: Dictionary<uint,Texture2D>[32] - legacy cached raw PNG textures by asset hash - owner: ModAssetManager
        private static readonly Dictionary<uint, Texture2D> _rawTextures = new Dictionary<uint, Texture2D>(32);
        // COLD ALLOC: HashSet<uint>[128] - FNV-hashed MOD_COMPATIBLE ledger prefab references - owner: ModAssetManager
        private static readonly HashSet<uint> _modCompatibleAssetHashes = new HashSet<uint>(128);
        private const string ModCompatibleLedgerTag = "MOD_COMPATIBLE";
        private const long MaxRawTextureBytes = 8L * 1024L * 1024L;
        private const int MaxRawTextureDimension = 2048;
        private const string MaxRawTextureBytesLabel = "8388608";
        private const string MaxRawTextureDimensionLabel = "2048";
        private static readonly Encoding LedgerEncoding = new UTF8Encoding(false);
        private static bool _modCompatibleLedgerLoaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            UnloadAllBundles();
            _bundlePaths.Clear();
            _rawTextures.Clear();
            _modCompatibleAssetHashes.Clear();
            _modCompatibleLedgerLoaded = false;
        }

        /// <summary>
        /// Registers the primary AssetBundle path resolved for a mod package.
        /// </summary>
        /// <param name="modId">Stable mod identifier.</param>
        /// <param name="bundlePath">Absolute bundle path or an empty value when no bundle exists.</param>
        internal static void RegisterBundlePath(string modId, string bundlePath)
        {
            if (string.IsNullOrWhiteSpace(modId))
                return;

            uint modHash = ModCommandDispatcher.ComputeModHash(modId);
            if (ModLoader.GetIsFutureCommandEnvelopeOnly())
            {
                _bundlePaths.Remove(modHash);
                return;
            }

            if (string.IsNullOrWhiteSpace(bundlePath))
            {
                _bundlePaths.Remove(modHash);
                return;
            }

            _bundlePaths[modHash] = bundlePath;
        }

        /// <summary>
        /// Legacy load from a registered AssetBundle; returns null while envelope-only UGC is enforced.
        /// </summary>
        internal static GameObject LoadPrefab(string modId, string assetName)
        {
            return LoadAsset<GameObject>(modId, assetName);
        }

        /// <summary>
        /// Legacy load from a registered AssetBundle; returns null while envelope-only UGC is enforced.
        /// </summary>
        internal static AudioClip LoadAudioClip(string modId, string assetName)
        {
            return LoadAsset<AudioClip>(modId, assetName);
        }

        /// <summary>
        /// Legacy load from a registered AssetBundle or raw PNG fallback; returns null while envelope-only UGC is enforced.
        /// </summary>
        internal static Texture2D LoadTexture(string modId, string assetName)
        {
            Texture2D texture = LoadAsset<Texture2D>(modId, assetName);
            if (texture != null)
                return texture;

            return LoadRawTexture(modId, assetName);
        }

        private static TAsset LoadAsset<TAsset>(string modId, string assetName)
            where TAsset : UnityEngine.Object
        {
            if (ModLoader.GetIsFutureCommandEnvelopeOnly())
                return null;

            if (string.IsNullOrWhiteSpace(modId) || string.IsNullOrWhiteSpace(assetName))
                return null;

            if (typeof(TAsset) == typeof(GameObject) &&
                IsProjectPrefabReference(assetName) &&
                !IsLedgerModCompatible(assetName))
            {
                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModAssetManager] SECURITY_VIOLATION: mod '", modId, "' attempted to load unauthorized prefab reference '", assetName, "'."));
                return null;
            }

            uint modHash = ModCommandDispatcher.ComputeModHash(modId);
            if (!TryGetLoadedBundle(modHash, modId, out AssetBundle bundle))
                return null;

            TAsset asset = bundle.LoadAsset<TAsset>(assetName);
            if (asset != null)
                return asset;

            string[] assetNames = bundle.GetAllAssetNames();
            if (assetNames == null || assetNames.Length == 0)
                return null;

            for (int i = 0; i < assetNames.Length; i++)
            {
                string candidate = assetNames[i];
                if (candidate == null)
                    continue;

                if (!EndsWithAssetPath(candidate, assetName))
                    continue;

                asset = bundle.LoadAsset<TAsset>(candidate);
                if (asset != null)
                    return asset;
            }

            Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModAssetManager] Asset '", assetName, "' was not found in bundle for mod '", modId, "'."));
            return null;
        }

        private static bool TryGetLoadedBundle(uint modHash, string modId, out AssetBundle bundle)
        {
            if (_loadedBundles.TryGetValue(modHash, out bundle) && bundle != null)
                return true;

            bundle = null;
            if (!_bundlePaths.TryGetValue(modHash, out string bundlePath) ||
                string.IsNullOrWhiteSpace(bundlePath) ||
                !File.Exists(bundlePath))
            {
                return false;
            }

            bundle = AssetBundle.LoadFromFile(bundlePath);
            if (bundle == null)
            {
                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModAssetManager] Failed to load AssetBundle '", bundlePath, "' for mod '", modId, "'."));
                return false;
            }

            _loadedBundles[modHash] = bundle;
            return true;
        }

        private static Texture2D LoadRawTexture(string modId, string assetName)
        {
            if (ModLoader.GetIsFutureCommandEnvelopeOnly())
                return null;

            if (string.IsNullOrWhiteSpace(assetName) || !assetName.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
                return null;

            if (!ModLoader.TryGetModDirectory(modId, out string modDirectory) || string.IsNullOrWhiteSpace(modDirectory))
                return null;

            if (!TryBuildModRelativeFilePath(modDirectory, assetName, out string filePath))
                return null;

            if (!File.Exists(filePath))
                return null;

            if (!TryValidateRawTextureFile(filePath))
                return null;

            uint cacheKey = ComputeAssetCacheHash(modId, filePath);
            if (_rawTextures.TryGetValue(cacheKey, out Texture2D cachedTexture) && cachedTexture != null)
                return cachedTexture;

            byte[] pngBytes;
            try
            {
                pngBytes = File.ReadAllBytes(filePath);
            }
            catch (UnauthorizedAccessException exception)
            {
                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModAssetManager] Rejected inaccessible raw texture '", filePath, "': ", exception.Message));
                return null;
            }
            catch (IOException exception)
            {
                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModAssetManager] Failed to read raw texture '", filePath, "': ", exception.Message));
                return null;
            }
            catch (System.Exception exception)
            {
                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModAssetManager] Rejected invalid raw texture read '", filePath, "': ", exception.Message));
                return null;
            }

            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, true, false)
            {
                name = Path.GetFileNameWithoutExtension(filePath)
            }; // COLD ALLOC: Texture2D[1] - legacy raw PNG fallback for mod texture loading - owner: ModAssetManager

            if (!ImageConversion.LoadImage(texture, pngBytes, false))
            {
                UnityEngine.Object.Destroy(texture);
                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModAssetManager] PNG decode failed for '", filePath, "'."));
                return null;
            }

            if (texture.width > MaxRawTextureDimension || texture.height > MaxRawTextureDimension)
            {
                UnityEngine.Object.Destroy(texture);
                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModAssetManager] Raw texture '", filePath, "' exceeded ", MaxRawTextureDimensionLabel, "px dimension cap."));
                return null;
            }

            _rawTextures[cacheKey] = texture;
            return texture;
        }

        private static bool TryValidateRawTextureFile(string filePath)
        {
            try
            {
                // COLD ALLOC: FileInfo[1] — raw texture size gate — owner: ModAssetManager
                var fileInfo = new FileInfo(filePath);
                if (!fileInfo.Exists || fileInfo.Length <= 0L)
                    return false;

                if (fileInfo.Length > MaxRawTextureBytes)
                {
                    Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModAssetManager] Raw texture '", filePath, "' exceeded ", MaxRawTextureBytesLabel, " byte cap."));
                    return false;
                }
            }
            catch (IOException exception)
            {
                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModAssetManager] Failed to inspect raw texture '", filePath, "': ", exception.Message));
                return false;
            }
            catch (System.Exception exception)
            {
                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModAssetManager] Rejected invalid raw texture '", filePath, "': ", exception.Message));
                return false;
            }

            return true;
        }

        private static void UnloadAllBundles()
        {
            Dictionary<uint, AssetBundle>.Enumerator enumerator = _loadedBundles.GetEnumerator();
            while (enumerator.MoveNext())
            {
                AssetBundle bundle = enumerator.Current.Value;
                if (bundle != null)
                    bundle.Unload(false);
            }

            _loadedBundles.Clear();

            Dictionary<uint, Texture2D>.Enumerator textureEnumerator = _rawTextures.GetEnumerator();
            while (textureEnumerator.MoveNext())
            {
                Texture2D texture = textureEnumerator.Current.Value;
                if (texture != null)
                    UnityEngine.Object.Destroy(texture);
            }

            _rawTextures.Clear();
        }

        private static uint ComputeAssetCacheHash(string modId, string filePath)
        {
            unchecked
            {
                uint hash = ModCommandDispatcher.ComputeModHash(modId);
                hash ^= ModCommandDispatcher.ComputeModHash(filePath) + 0x9E3779B9u + (hash << 6) + (hash >> 2);
                return hash;
            }
        }

        private static bool IsProjectPrefabReference(string assetName)
        {
            return StartsWithAssetsPrefix(assetName) ||
                   IsGuidLike(assetName);
        }

        private static bool IsLedgerModCompatible(string assetName)
        {
            EnsureModCompatibleLedgerLoaded();

            uint assetHash = ComputeNormalizedAssetReferenceHash(assetName);
            return assetHash != 0u && _modCompatibleAssetHashes.Contains(assetHash);
        }

        private static void EnsureModCompatibleLedgerLoaded()
        {
            if (_modCompatibleLedgerLoaded)
                return;

            _modCompatibleLedgerLoaded = true;
            string ledgerPath = Path.Combine(Application.dataPath, "..", "Docs", "ARCHITECTURE", "PROJECT_CONTENT_LEDGER.md");
            if (!File.Exists(ledgerPath))
                return;

            try
            {
                // COLD ALLOC: StreamReader[1] - sequential project content ledger scan for mod prefab allowlist - owner: ModAssetManager
                using (StreamReader reader = new StreamReader(ledgerPath, LedgerEncoding, detectEncodingFromByteOrderMarks: true))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.IndexOf(ModCompatibleLedgerTag, System.StringComparison.OrdinalIgnoreCase) < 0)
                            continue;

                        RegisterLedgerAssetReferences(line);
                    }
                }
            }
            catch (IOException exception)
            {
                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModAssetManager] Failed to scan project content ledger for mod allowlist: ", exception.Message));
            }
        }

        private static void RegisterLedgerAssetReferences(string line)
        {
            int assetStart = line.IndexOf("Assets/", System.StringComparison.OrdinalIgnoreCase);
            if (assetStart >= 0)
            {
                int assetEnd = assetStart;
                while (assetEnd < line.Length)
                {
                    char c = line[assetEnd];
                    if (char.IsWhiteSpace(c) || c == '|' || c == ')' || c == ']' || c == '`' || c == '"')
                        break;

                    assetEnd++;
                }

                if (assetEnd > assetStart)
                    RegisterLedgerAssetReference(line, assetStart, assetEnd - assetStart);
            }

            for (int i = 0; i <= line.Length - 32; i++)
            {
                if (IsGuidLike(line, i, 32))
                    RegisterLedgerAssetReference(line, i, 32);
            }
        }

        private static void RegisterLedgerAssetReference(string source, int start, int length)
        {
            uint assetHash = ComputeNormalizedAssetReferenceHash(source, start, length);
            if (assetHash != 0u)
                _modCompatibleAssetHashes.Add(assetHash);
        }

        private static uint ComputeNormalizedAssetReferenceHash(string assetName)
        {
            return string.IsNullOrWhiteSpace(assetName)
                ? 0u
                : ComputeNormalizedAssetReferenceHash(assetName, 0, assetName.Length);
        }

        private static uint ComputeNormalizedAssetReferenceHash(string source, int start, int length)
        {
            if (string.IsNullOrEmpty(source) || length <= 0 || start < 0 || start + length > source.Length)
                return 0u;

            unchecked
            {
                uint hash = LocHash.FnvOffsetBasis;
                for (int i = 0; i < length; i++)
                {
                    char current = NormalizeAssetPathChar(source[start + i]);
                    hash ^= (byte)current;
                    hash *= LocHash.FnvPrime;
                    hash ^= (byte)(current >> 8);
                    hash *= LocHash.FnvPrime;
                }

                return hash;
            }
        }

        private static bool EndsWithAssetPath(string candidate, string requested)
        {
            if (string.IsNullOrEmpty(candidate) ||
                string.IsNullOrEmpty(requested) ||
                candidate.Length < requested.Length)
            {
                return false;
            }

            int offset = candidate.Length - requested.Length;
            for (int i = 0; i < requested.Length; i++)
            {
                if (NormalizeAssetPathChar(candidate[offset + i]) != NormalizeAssetPathChar(requested[i]))
                    return false;
            }

            return true;
        }

        private static bool StartsWithAssetsPrefix(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length < 7)
                return false;

            return NormalizeAssetPathChar(value[0]) == 'a' &&
                   NormalizeAssetPathChar(value[1]) == 's' &&
                   NormalizeAssetPathChar(value[2]) == 's' &&
                   NormalizeAssetPathChar(value[3]) == 'e' &&
                   NormalizeAssetPathChar(value[4]) == 't' &&
                   NormalizeAssetPathChar(value[5]) == 's' &&
                   NormalizeAssetPathChar(value[6]) == '/';
        }

        private static char NormalizeAssetPathChar(char value)
        {
            if (value == '\\')
                return '/';

            return ToAsciiLower(value);
        }

        private static char ToAsciiLower(char value)
        {
            return value >= 'A' && value <= 'Z' ? (char)(value + 32) : value;
        }

        private static bool TryBuildModRelativeFilePath(string modDirectory, string relativePath, out string filePath)
        {
            filePath = null;
            if (string.IsNullOrWhiteSpace(modDirectory) ||
                string.IsNullOrWhiteSpace(relativePath) ||
                Path.IsPathRooted(relativePath))
            {
                return false;
            }

            string fullDirectory;
            string candidatePath;
            try
            {
                fullDirectory = Path.GetFullPath(modDirectory);
                candidatePath = Path.GetFullPath(Path.Combine(fullDirectory, relativePath));
            }
            catch (System.Exception exception)
            {
                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModAssetManager] Rejected invalid raw texture path '", relativePath, "': ", exception.Message));
                return false;
            }

            if (!IsSameOrChildPath(candidatePath, fullDirectory))
                return false;

            filePath = candidatePath;
            return true;
        }

        private static bool IsSameOrChildPath(string candidatePath, string directoryPath)
        {
            if (string.IsNullOrEmpty(candidatePath) || string.IsNullOrEmpty(directoryPath))
                return false;

            int directoryLength = directoryPath.Length;
            while (directoryLength > 0 && IsDirectorySeparator(directoryPath[directoryLength - 1]))
                directoryLength--;

            if (directoryLength <= 0 || candidatePath.Length < directoryLength)
                return false;

            for (int i = 0; i < directoryLength; i++)
            {
                if (NormalizeFileSystemPathChar(candidatePath[i]) != NormalizeFileSystemPathChar(directoryPath[i]))
                    return false;
            }

            return candidatePath.Length == directoryLength || IsDirectorySeparator(candidatePath[directoryLength]);
        }

        private static char NormalizeFileSystemPathChar(char value)
        {
            if (value == '\\')
                return '/';

            return ToAsciiLower(value);
        }

        private static bool IsDirectorySeparator(char value)
        {
            return value == '/' || value == '\\';
        }

        private static bool IsGuidLike(string value)
        {
            if (value.Length != 32)
                return false;

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                bool isHex = (c >= '0' && c <= '9') ||
                             (c >= 'a' && c <= 'f') ||
                             (c >= 'A' && c <= 'F');
                if (!isHex)
                    return false;
            }

            return true;
        }

        private static bool IsGuidLike(string value, int start, int length)
        {
            if (string.IsNullOrEmpty(value) || length != 32 || start < 0 || start + length > value.Length)
                return false;

            for (int i = 0; i < length; i++)
            {
                char c = value[start + i];
                bool isHex = (c >= '0' && c <= '9') ||
                             (c >= 'a' && c <= 'f') ||
                             (c >= 'A' && c <= 'F');
                if (!isHex)
                    return false;
            }

            return true;
        }
    }
}
