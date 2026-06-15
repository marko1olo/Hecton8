using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace Hecton8.Modding
{
    /// <summary>
    /// Addressables-based owner for mod content assets.
    /// Envelope-only UGC mode disables registration and loading; active assets must pass the FutureCommandEnvelope CRC gate.
    /// </summary>
    internal static class ModAssetManager
    {
        // COLD ALLOC: Dictionary<uint, AsyncOperationHandle>[32] - mod hash to loaded catalog handle - owner: ModAssetManager
        private static readonly Dictionary<uint, AsyncOperationHandle<IResourceLocator>> _loadedCatalogs = new Dictionary<uint, AsyncOperationHandle<IResourceLocator>>(32);
        
        // COLD ALLOC: Dictionary<uint, List<AsyncOperationHandle>>[32] - mod hash to loaded asset handles - owner: ModAssetManager
        private static readonly Dictionary<uint, List<AsyncOperationHandle>> _loadedAssetHandles = new Dictionary<uint, List<AsyncOperationHandle>>(32);

        // COLD ALLOC: HashSet<uint>[128] - FNV-hashed MOD_COMPATIBLE ledger prefab references - owner: ModAssetManager
        private static readonly HashSet<uint> _modCompatibleAssetHashes = new HashSet<uint>(128);
        private const string ModCompatibleLedgerTag = "MOD_COMPATIBLE";
        private static readonly Encoding LedgerEncoding = new UTF8Encoding(false);
        private static bool _modCompatibleLedgerLoaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            UnloadAllCatalogs();
            _loadedCatalogs.Clear();
            _loadedAssetHandles.Clear();
            _modCompatibleAssetHashes.Clear();
            _modCompatibleLedgerLoaded = false;
        }

        /// <summary>
        /// Registers the Addressables catalog resolved for a mod package and loads it synchronously.
        /// </summary>
        /// <param name="modId">Stable mod identifier.</param>
        /// <param name="catalogPath">Absolute catalog.json path or an empty value when no catalog exists.</param>
        internal static void RegisterCatalogPath(string modId, string catalogPath)
        {
            if (string.IsNullOrWhiteSpace(modId))
                return;

            uint modHash = ModCommandDispatcher.ComputeModHash(modId);
            if (ModLoader.GetIsFutureCommandEnvelopeOnly())
            {
                UnloadModAssets(modHash);
                return;
            }

            if (string.IsNullOrWhiteSpace(catalogPath) || !File.Exists(catalogPath))
            {
                UnloadModAssets(modHash);
                return;
            }

            if (_loadedCatalogs.ContainsKey(modHash))
                UnloadModAssets(modHash);

            try
            {
                AsyncOperationHandle<IResourceLocator> handle = Addressables.LoadContentCatalogAsync(catalogPath);
                handle.WaitForCompletion();
                
                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    _loadedCatalogs[modHash] = handle;
                }
                else
                {
                    Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModAssetManager] Failed to load catalog '", catalogPath, "' for mod '", modId, "'."));
                    Addressables.Release(handle);
                }
            }
            catch (System.Exception ex)
            {
                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModAssetManager] Exception loading catalog '", catalogPath, "' for mod '", modId, "': ", ex.Message));
            }
        }

        /// <summary>
        /// Removes the Catalog binding for a mod and releases its cached catalog and assets.
        /// </summary>
        /// <param name="modId">Stable mod identifier.</param>
        internal static void UnregisterCatalogPath(string modId)
        {
            if (string.IsNullOrWhiteSpace(modId))
                return;

            uint modHash = ModCommandDispatcher.ComputeModHash(modId);
            UnloadModAssets(modHash);
        }

        internal static GameObject LoadPrefab(string modId, string assetName)
        {
            return LoadAsset<GameObject>(modId, assetName);
        }

        internal static AudioClip LoadAudioClip(string modId, string assetName)
        {
            return LoadAsset<AudioClip>(modId, assetName);
        }

        internal static Texture2D LoadTexture(string modId, string assetName)
        {
            return LoadAsset<Texture2D>(modId, assetName);
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
            if (!_loadedCatalogs.ContainsKey(modHash))
                return null;

            try
            {
                AsyncOperationHandle<TAsset> handle = Addressables.LoadAssetAsync<TAsset>(assetName);
                handle.WaitForCompletion();

                if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
                {
                    if (!_loadedAssetHandles.TryGetValue(modHash, out List<AsyncOperationHandle> handles))
                    {
                        handles = new List<AsyncOperationHandle>(16);
                        _loadedAssetHandles[modHash] = handles;
                    }
                    handles.Add(handle);
                    return handle.Result;
                }
                else
                {
                    Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModAssetManager] Asset '", assetName, "' was not found in Addressables for mod '", modId, "'."));
                    if (handle.IsValid())
                        Addressables.Release(handle);
                    return null;
                }
            }
            catch (System.Exception ex)
            {
                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModAssetManager] Failed to load Addressable asset '", assetName, "' for mod '", modId, "': ", ex.Message));
                return null;
            }
        }

        private static void UnloadAllCatalogs()
        {
            foreach (var kvp in _loadedAssetHandles)
            {
                if (kvp.Value == null) continue;
                for (int i = 0; i < kvp.Value.Count; i++)
                {
                    if (kvp.Value[i].IsValid())
                        Addressables.Release(kvp.Value[i]);
                }
            }
            _loadedAssetHandles.Clear();

            foreach (var kvp in _loadedCatalogs)
            {
                if (kvp.Value.IsValid())
                    Addressables.Release(kvp.Value);
            }
            _loadedCatalogs.Clear();
        }

        private static void UnloadModAssets(uint modHash)
        {
            if (_loadedAssetHandles.TryGetValue(modHash, out List<AsyncOperationHandle> handles) && handles != null)
            {
                for (int i = 0; i < handles.Count; i++)
                {
                    if (handles[i].IsValid())
                        Addressables.Release(handles[i]);
                }
                _loadedAssetHandles.Remove(modHash);
            }

            if (_loadedCatalogs.TryGetValue(modHash, out AsyncOperationHandle<IResourceLocator> catalogHandle))
            {
                if (catalogHandle.IsValid())
                    Addressables.Release(catalogHandle);
                _loadedCatalogs.Remove(modHash);
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
                int assetEnd = line.IndexOf(".prefab", assetStart, System.StringComparison.OrdinalIgnoreCase);
                if (assetEnd > assetStart)
                {
                    string assetPath = line.Substring(assetStart, assetEnd - assetStart + 7);
                    uint assetHash = ModCommandDispatcher.ComputeModHash(assetPath);
                    if (assetHash != 0u)
                        _modCompatibleAssetHashes.Add(assetHash);
                }
            }

            int guidStart = line.IndexOf("guid:", System.StringComparison.OrdinalIgnoreCase);
            if (guidStart >= 0)
            {
                guidStart += 5;
                int guidEnd = guidStart;
                while (guidEnd < line.Length)
                {
                    char c = line[guidEnd];
                    if ((c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F') || (c >= '0' && c <= '9'))
                        guidEnd++;
                    else
                        break;
                }

                if (guidEnd - guidStart == 32)
                {
                    string guidString = line.Substring(guidStart, 32);
                    uint guidHash = ModCommandDispatcher.ComputeModHash(guidString);
                    if (guidHash != 0u)
                        _modCompatibleAssetHashes.Add(guidHash);
                }
            }
        }

        private static uint ComputeNormalizedAssetReferenceHash(string assetName)
        {
            if (string.IsNullOrWhiteSpace(assetName))
                return 0u;

            if (IsGuidLike(assetName))
                return ModCommandDispatcher.ComputeModHash(assetName.ToLowerInvariant());

            string normalizedPath = assetName.Replace('\\', '/');
            if (normalizedPath.StartsWith("assets/", System.StringComparison.OrdinalIgnoreCase))
                return ModCommandDispatcher.ComputeModHash(normalizedPath);

            return 0u;
        }

        private static bool StartsWithAssetsPrefix(string assetName)
        {
            if (string.IsNullOrWhiteSpace(assetName) || assetName.Length < 7)
                return false;

            return (assetName[0] == 'A' || assetName[0] == 'a') &&
                   (assetName[1] == 'S' || assetName[1] == 's') &&
                   (assetName[2] == 'S' || assetName[2] == 's') &&
                   (assetName[3] == 'E' || assetName[3] == 'e') &&
                   (assetName[4] == 'T' || assetName[4] == 't') &&
                   (assetName[5] == 'S' || assetName[5] == 's') &&
                   (assetName[6] == '/' || assetName[6] == '\\');
        }

        private static bool IsGuidLike(string assetName)
        {
            if (string.IsNullOrWhiteSpace(assetName) || assetName.Length != 32)
                return false;

            for (int i = 0; i < 32; i++)
            {
                char c = assetName[i];
                if ((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))
                    continue;

                return false;
            }

            return true;
        }
    }
}
