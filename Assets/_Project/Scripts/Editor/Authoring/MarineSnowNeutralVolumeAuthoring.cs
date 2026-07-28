// ============================================================================
// HECTON-8 — MarineSnowNeutralVolumeAuthoring.cs
// Creates the two authored neutral Texture3D volumes that
// HectonMarineSnowRenderer.EnsureBuffers requires and that do not exist
// anywhere on disk. Without them the renderer takes the
// DisableAfterUnrecoverableSetupFailure branch and fires two
// UnityEngine.Assertions.Assert.IsNotNull failures once per ColdTick poll
// (1 Hz) for the whole gameplay phase, and there is no marine snow at all.
//
// WHY A SECOND GENERATOR WHEN ProceduralTextureBaker ALREADY HAS ONE:
//   ParticulateFlipbookBaker.cs:127/134 already bakes both assets, but only as
//   step 10 and 11 of BakeRequiredParticulateFlipbooks1728, which first bakes
//   three full procedural particle flipbooks and is wrapped in a rollback
//   transaction (ParticulateFlipbookBaker.cs:100-139). Any failure in the silt,
//   snow or cavitation bake calls TryRestoreAssetFileRollbackSnapshots and
//   throws BOTH neutral volumes away with it. Two 8-byte lookup textures must
//   not be hostage to a heavyweight art bake, so this is the narrow lane.
//
// WHY THE NEUTRAL VALUES HERE DIFFER FROM THAT BAKER — READ BEFORE "FIXING":
//   Cave SDF, R = 1.0 (not 0.5):
//     Hecton_MarineSnow.compute:918 reads .r and :919 decodes it as
//       lerp(-_HectonCaveVoxelHalfExtents.w, +_HectonCaveVoxelHalfExtents.w, r)
//     so r is a [0,1]-packed SIGNED distance where 0 is deep solid and 1 is
//     maximum free water. ResolveSdfParticleCollision (:922-926) treats any
//     negative result as a collision that freezes the particle and drains its
//     life. HectonCaveVoxelLightingVolume.cs:900-901 states the project's own
//     definition of an empty SDF volume outright:
//       byte fill = foundOccupied ? byte.MinValue : byte.MaxValue;
//     i.e. "no occupied voxel anywhere" bakes to byte.MaxValue == 255 == 1.0.
//     ParticulateFlipbookBaker.cs:127 uses 0.5, which decodes to distance
//     EXACTLY 0.0 - every texel reads as "standing on a cave surface". That
//     also poisons CS_EvaluateWakeProximity (:1443-1449), which takes
//     abs(distance) as proximity, so 0.5 reports "touching rock everywhere"
//     while 1.0 correctly reports "nothing nearby".
//     Independent corroboration: ProceduralScatterRenderer.cs:305-308, a
//     different consumer of the same published cave SDF, spells its own
//     fallback as new Texture3D(1, 1, 1, TextureFormat.R8, false) +
//     SetPixelData(new byte[] { 255 }, 0) - byte-for-byte the choice below.
//   Abyssal flow, RGBA = 0 (not 0.5):
//     Hecton_MarineSnow.compute:394 reads the volume as
//       float3 textureFlow = _AbyssalFlowFieldTexture.Load(int4(coord, 0)).xyz;
//     and :396-397 assigns it straight into resolvedFlow with NO *2-1 unbias
//     step anywhere in the file. The value is a raw world-space velocity, and
//     HectonFluidEngine.cs:8404 publishes the real volume as
//     R16G16B16A16_SFloat precisely because it is signed and needs no bias.
//     ParticulateFlipbookBaker.cs:134 uses (0.5, 0.5, 0.5, 0), which is not
//     "no current" - it is a constant 0.5 m/s drift along +X+Y+Z.
//   Neither wrong value is reachable today (see the honesty note below), so
//   this is a latent-defect divergence, not a live bug fix. If the lead ever
//   runs BakeRequiredParticulateFlipbooks1728 it will CopySerialized the 0.5
//   values back over these assets (ParticulateFlipbookBaker.cs:190) and
//   silently revert both format and content.
//
// FORMATS ARE COPIED FROM THE PRODUCERS, NOT CHOSEN:
//   Cave SDF     -> TextureFormat.R8, matching the live volume at
//                   HectonCaveVoxelLightingVolume.cs:135/553, and satisfying
//                   the single-channel `Texture3D<float>` at compute:81.
//   Abyssal flow -> TextureFormat.RGBAHalf, the TextureFormat twin of the live
//                   R16G16B16A16_SFloat at HectonFluidEngine.cs:8404, and
//                   satisfying the `Texture3D<float4>` at compute:73 whose
//                   reader needs three real channels.
//
// Apply(makeNoLongerReadable: false) is deliberate. These are serialized
// assets, not runtime uploads; discarding the CPU copy before
// AssetDatabase.CreateAsset risks writing an asset with no image data. The
// cost of keeping it is 1 byte and 8 bytes.
//
// HONESTY NOTE - WHAT THIS DOES AND DOES NOT BUY:
//   Both volumes are bound ONLY on the inactive path. RefreshCaveSdfBinding
//   (HectonMarineSnowRenderer.cs:4431-4450) starts from the empty texture with
//   active = 0 and only reaches active = 1 by taking a published payload, and
//   HectonCaveVoxelLightingVolume.cs:387-392 cannot return true with a null
//   texture. RefreshAbyssalFlowBinding (:4208-4245) is the same shape against
//   HectonFluidEngine.cs:2739-2747. Both shader readers bail on their active
//   flag (compute:906 and :387) BEFORE touching the volume. So the CONTENT of
//   these two assets is genuinely never sampled while they are the fallback -
//   their existence is what matters. This unblocks the renderer; it does not
//   by itself make marine snow look like anything.
// ============================================================================

using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.Authoring
{
    /// <summary>
    /// Generates the two 1x1x1 neutral marine-snow lookup volumes. Idempotent:
    /// an existing asset at either path is left exactly as it is.
    /// </summary>
    public static class MarineSnowNeutralVolumeAuthoring
    {
        private const string Marker = "[H8_MARINE_SNOW_VOLUMES]";

        /// <summary>
        /// Byte-identical to HectonMarineSnowRenderer.cs:48-49. The renderer's
        /// editor-only self-heal (RefreshAuthoredNeutralVolumeFallbacksColdEditor,
        /// HectonMarineSnowRenderer.cs:5059-5068) does
        /// AssetDatabase.LoadAssetAtPath&lt;Texture3D&gt; against these exact
        /// strings, so the generated assets have to answer to them.
        /// </summary>
        private const string RendererExpectedCaveSdfPath =
            "Assets/_Project/Art/Textures/VFX/ParticulateFlipbooks1728/TX_MarineSnow_EmptyCaveSdf_1x1x1.asset";

        private const string RendererExpectedAbyssalFlowPath =
            "Assets/_Project/Art/Textures/VFX/ParticulateFlipbooks1728/TX_MarineSnow_EmptyAbyssalFlow_1x1x1.asset";

        /// <summary>
        /// Same folder ParticulateFlipbookBaker.cs:11 owns. Not invented here.
        /// </summary>
        private const string RequestedOutputFolder = "Assets/_Project/Art/Textures/VFX/ParticulateFlipbooks1728";

        /// <summary>Names are law: ParticulateFlipbookBaker.cs:12-13, TX_ prefix.</summary>
        private const string CaveSdfAssetName = "TX_MarineSnow_EmptyCaveSdf_1x1x1.asset";

        private const string AbyssalFlowAssetName = "TX_MarineSnow_EmptyAbyssalFlow_1x1x1.asset";

        /// <summary>
        /// R8, one texel, 0xFF. 255 == 1.0 unorm == maximum free water, the
        /// value HectonCaveVoxelLightingVolume.cs:900-901 itself writes for a
        /// volume with no occupied voxel.
        /// </summary>
        private static readonly byte[] NeutralCaveSdfTexel = { 0xFF };

        /// <summary>
        /// RGBAHalf, one texel, four halves of 0x0000 == exactly 0.0 each ==
        /// zero world-space current. Read raw at compute:394.
        /// </summary>
        private static readonly byte[] NeutralAbyssalFlowTexel = { 0, 0, 0, 0, 0, 0, 0, 0 };

        [MenuItem("Hecton8/VFX/Generate Marine Snow Neutral Volumes")]
        public static void GenerateNeutralMarineSnowVolumes()
        {
            if (!TryResolveOutputFolderWithRealCasing(RequestedOutputFolder, out string outputFolder, out string folderFailure))
            {
                Debug.LogError($"{Marker} ABORT - {folderFailure}");
                return;
            }

            string caveSdfPath = outputFolder + "/" + CaveSdfAssetName;
            string abyssalFlowPath = outputFolder + "/" + AbyssalFlowAssetName;

            if (!TryEnsureNeutralVolume(
                    caveSdfPath,
                    TextureFormat.R8,
                    NeutralCaveSdfTexel,
                    out string caveOutcome,
                    out string caveFailure))
            {
                Debug.LogError($"{Marker} ABORT - {caveFailure}");
                return;
            }

            if (!TryEnsureNeutralVolume(
                    abyssalFlowPath,
                    TextureFormat.RGBAHalf,
                    NeutralAbyssalFlowTexel,
                    out string flowOutcome,
                    out string flowFailure))
            {
                Debug.LogError($"{Marker} ABORT - {flowFailure}");
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // The only claim worth making: does the renderer's own literal path
            // resolve? The on-disk folder is TEXTURES while the renderer
            // constant says Textures, so this is the line that proves the
            // case-insensitive lookup actually lands.
            bool rendererSeesCaveSdf = AssetDatabase.LoadAssetAtPath<Texture3D>(RendererExpectedCaveSdfPath) != null;
            bool rendererSeesAbyssalFlow = AssetDatabase.LoadAssetAtPath<Texture3D>(RendererExpectedAbyssalFlowPath) != null;

            Debug.Log($"{Marker} folder='{outputFolder}' caveSdf={caveOutcome} abyssalFlow={flowOutcome} " +
                      $"rendererPathResolves={(rendererSeesCaveSdf && rendererSeesAbyssalFlow)} " +
                      $"(caveSdf={rendererSeesCaveSdf} abyssalFlow={rendererSeesAbyssalFlow})");

            if (!rendererSeesCaveSdf || !rendererSeesAbyssalFlow)
            {
                Debug.LogError($"{Marker} assets exist but HectonMarineSnowRenderer's literal path does not resolve - " +
                               "the folder casing differs and AssetDatabase did not fold it. The two serialized " +
                               "Texture3D fields must be assigned by hand instead of relying on the editor self-heal.");
            }
        }

        /// <summary>
        /// Creates the asset only when the path is empty. An existing asset -
        /// of any type - is reported and left untouched, so a second run
        /// changes nothing and an artist's replacement is never clobbered.
        /// </summary>
        private static bool TryEnsureNeutralVolume(
            string assetPath,
            TextureFormat format,
            byte[] texelBytes,
            out string outcome,
            out string failure)
        {
            outcome = string.Empty;
            failure = string.Empty;

            var occupant = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (occupant != null)
            {
                var existingVolume = occupant as Texture3D;
                outcome = existingVolume != null
                    ? "KEPT-EXISTING"
                    : "DECLINED-PATH-OCCUPIED-BY-" + occupant.GetType().Name;
                return true;
            }

            Texture3D texture = null;
            try
            {
                // COLD ALLOC: Texture3D[1] - single-texel neutral lookup volume authored once in the editor - owner: MarineSnowNeutralVolumeAuthoring
                texture = new Texture3D(1, 1, 1, format, false)
                {
                    name = Path.GetFileNameWithoutExtension(assetPath),
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Point,
                    anisoLevel = 0
                };
                texture.SetPixelData(texelBytes, 0);
                texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);

                AssetDatabase.CreateAsset(texture, assetPath);
                texture = null; // ownership handed to the AssetDatabase
                // Explicit ToString rather than implicit boxing in a concat. Cold
                // editor path, so Enum.ToString is permitted here - it is banned
                // in tick/render/UI cadence, not in a menu-driven one-shot.
                outcome = "CREATED-" + format.ToString();
                return true;
            }
            catch (Exception ex) when (ex is UnityException ||
                                      ex is IOException ||
                                      ex is UnauthorizedAccessException ||
                                      ex is InvalidOperationException ||
                                      ex is ArgumentException ||
                                      ex is NotSupportedException)
            {
                failure = "neutral volume creation failed for " + assetPath + ": " +
                          ex.GetType().Name + ": " + ex.Message;
                return false;
            }
            finally
            {
                if (texture != null)
                    UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        /// <summary>
        /// Walks the requested chain segment by segment and adopts whatever
        /// casing already exists on disk, because Assets/_Project/Art is
        /// physically "TEXTURES" while every baker in the project - and
        /// HectonMarineSnowRenderer.cs:48-49 - spells it "Textures".
        /// AssetDatabase.CreateFolder("Assets/_Project/Art/Textures", "VFX")
        /// would otherwise be a coin flip on a case-folding detail. Discovery
        /// uses System.IO rather than AssetDatabase.GetSubFolders so nothing
        /// here depends on an API signature that has not been read.
        /// </summary>
        private static bool TryResolveOutputFolderWithRealCasing(
            string requestedFolder,
            out string resolvedFolder,
            out string failure)
        {
            resolvedFolder = string.Empty;
            failure = string.Empty;

            string normalized = requestedFolder.Replace('\\', '/');
            if (!normalized.StartsWith("Assets/", StringComparison.Ordinal))
            {
                failure = "output folder must live under Assets/ - got " + normalized;
                return false;
            }

            string[] segments = normalized.Split('/');
            string currentAssetPath = "Assets";
            string currentAbsolute = Application.dataPath.Replace('\\', '/');

            for (int i = 1; i < segments.Length; i++)
            {
                string segment = segments[i];
                if (segment.Length == 0)
                    continue;

                string existingName = FindChildDirectoryIgnoringCase(currentAbsolute, segment);
                if (existingName != null)
                {
                    currentAssetPath = currentAssetPath + "/" + existingName;
                    currentAbsolute = currentAbsolute + "/" + existingName;
                    continue;
                }

                string createdGuid = AssetDatabase.CreateFolder(currentAssetPath, segment);
                if (string.IsNullOrEmpty(createdGuid))
                {
                    failure = "AssetDatabase.CreateFolder failed for " + currentAssetPath + "/" + segment;
                    return false;
                }

                currentAssetPath = currentAssetPath + "/" + segment;
                currentAbsolute = currentAbsolute + "/" + segment;
            }

            if (!AssetDatabase.IsValidFolder(currentAssetPath))
            {
                failure = "resolved path is not a valid asset folder: " + currentAssetPath;
                return false;
            }

            resolvedFolder = currentAssetPath;
            return true;
        }

        private static string FindChildDirectoryIgnoringCase(string parentAbsolute, string childName)
        {
            if (!Directory.Exists(parentAbsolute))
                return null;

            // COLD ALLOC: string[] - one editor-only folder listing per path segment - owner: MarineSnowNeutralVolumeAuthoring
            string[] children = Directory.GetDirectories(parentAbsolute);
            for (int i = 0; i < children.Length; i++)
            {
                string name = Path.GetFileName(children[i]);
                if (string.Equals(name, childName, StringComparison.OrdinalIgnoreCase))
                    return name;
            }

            return null;
        }
    }
}
