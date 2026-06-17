using System;
using System.IO;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.Bakers
{
    public static partial class ProceduralTextureBaker
    {
        private const string DefaultParticleFlipbookOutputFolder1728 = "Assets/_Project/Art/Textures/VFX/ParticulateFlipbooks1728";
        private const string NeutralCaveSdfAssetName1728 = "TX_MarineSnow_EmptyCaveSdf_1x1x1.asset";
        private const string NeutralAbyssalFlowAssetName1728 = "TX_MarineSnow_EmptyAbyssalFlow_1x1x1.asset";

        [MenuItem("Hecton8/Bakers/1728/Bake Required Silt Snow Cavitation Flipbooks", false, 1728)]
        public static void BakeRequiredParticulateFlipbooks1728()
        {
            if (!TryBakeParticulateFlipbooks1728(1f, DefaultParticleFlipbookOutputFolder1728, out string failure))
            {
                UnityEngine.Debug.LogError("[ParticulateFlipbookBaker1728] " + failure);
                return;
            }

            UnityEngine.Debug.Log("[ParticulateFlipbookBaker1728] Baked silt, marine snow, and cavitation flipbooks.");
        }

        internal static bool TryBakeParticulateFlipbooks1728(float globalQualityWeight, string outputFolder, out string failure)
        {
            failure = string.Empty;
            float q = math.saturate(globalQualityWeight);

            if (!ValidateUnmanagedLayouts1718(out string layoutFailure))
            {
                failure = layoutFailure;
                return false;
            }

            ParticleBakeProfile silt = new ParticleBakeProfile(
                "abyssal_silt_flakes_1728",
                ParticleBakeKind.SiltCloud,
                17280001u ^ 0x681BC982u,
                q,
                math.lerp(2.35f, 3.15f, q),
                math.lerp(4.0f, 5.5f, q),
                math.lerp(4.8f, 6.5f, q),
                math.lerp(0.82f, 0.74f, q),
                math.lerp(1.15f, 1.65f, q),
                0.12f);

            ParticleBakeProfile snow = new ParticleBakeProfile(
                "organic_marine_snow_threads_1728",
                ParticleBakeKind.MarineSnow,
                17280001u ^ 0x9E3779B9u,
                q,
                math.lerp(3.25f, 4.35f, q),
                math.lerp(6.0f, 8.25f, q),
                math.lerp(5.8f, 8.2f, q),
                math.lerp(0.86f, 0.79f, q),
                math.lerp(1.65f, 2.25f, q),
                0.08f);

            ParticleBakeProfile cavitation = new ParticleBakeProfile(
                "cavitation_bubble_burst_1728",
                ParticleBakeKind.CavitationBubble,
                17280001u ^ 0xC2B2AE35u,
                q,
                math.lerp(2.0f, 2.8f, q),
                math.lerp(3.0f, 4.0f, q),
                math.lerp(6.5f, 9.5f, q),
                math.lerp(0.88f, 0.82f, q),
                math.lerp(0.95f, 1.4f, q),
                math.lerp(0.035f, 0.055f, q));

            if (!TryResolveParticleBakeAssetPaths1718(in silt, outputFolder, out ResolvedBakeSettings siltSettings, out ParticleBakeAssetPaths siltPaths, out failure, forceRequiredFrameGrid: true) ||
                !TryResolveParticleBakeAssetPaths1718(in snow, outputFolder, out ResolvedBakeSettings snowSettings, out ParticleBakeAssetPaths snowPaths, out failure, forceRequiredFrameGrid: true) ||
                !TryResolveParticleBakeAssetPaths1718(in cavitation, outputFolder, out ResolvedBakeSettings cavitationSettings, out ParticleBakeAssetPaths cavitationPaths, out failure, forceRequiredFrameGrid: true))
            {
                return false;
            }

            if (!TryResolveNeutralVolumeAssetPaths1728(outputFolder, out string neutralCaveSdfPath, out string neutralAbyssalFlowPath, out failure))
                return false;

            // COLD ALLOC: string[11] - rollback transaction paths for editor bake - owner: ProceduralTextureBaker
            string[] transactionalPaths =
            {
                siltPaths.MaskPath,
                siltPaths.NormalPath,
                siltPaths.MaterialPath,
                snowPaths.MaskPath,
                snowPaths.NormalPath,
                snowPaths.MaterialPath,
                cavitationPaths.MaskPath,
                cavitationPaths.NormalPath,
                cavitationPaths.MaterialPath,
                neutralCaveSdfPath,
                neutralAbyssalFlowPath
            };

            if (!TryCaptureAssetFileRollbackSnapshots(transactionalPaths, out AssetFileRollbackSnapshot[] rollback, out string rollbackFailure))
            {
                failure = "output rollback capture failed: " + rollbackFailure;
                return false;
            }

            if (!TryBakeParticleFlipbookProfile1718(in silt, in siltSettings, in siltPaths))
            {
                TryRestoreAssetFileRollbackSnapshots(rollback);
                failure = "silt flipbook bake failed";
                return false;
            }

            if (!TryBakeParticleFlipbookProfile1718(in snow, in snowSettings, in snowPaths))
            {
                TryRestoreAssetFileRollbackSnapshots(rollback);
                failure = "marine snow flipbook bake failed";
                return false;
            }

            if (!TryBakeParticleFlipbookProfile1718(in cavitation, in cavitationSettings, in cavitationPaths))
            {
                TryRestoreAssetFileRollbackSnapshots(rollback);
                failure = "cavitation flipbook bake failed";
                return false;
            }

            if (!TryBakeNeutralVolumeTexture1728(neutralCaveSdfPath, new Color(0.5f, 0f, 0f, 0f), out string neutralCaveFailure))
            {
                TryRestoreAssetFileRollbackSnapshots(rollback);
                failure = neutralCaveFailure;
                return false;
            }

            if (!TryBakeNeutralVolumeTexture1728(neutralAbyssalFlowPath, new Color(0.5f, 0.5f, 0.5f, 0f), out string neutralFlowFailure))
            {
                TryRestoreAssetFileRollbackSnapshots(rollback);
                failure = neutralFlowFailure;
                return false;
            }

            if (!TryFinalizeAssetDatabase("1728 particulate flipbook bake", out string finalizeFailure))
            {
                TryRestoreAssetFileRollbackSnapshots(rollback);
                failure = finalizeFailure;
                return false;
            }

            return true;
        }

        internal static bool TryResolveNeutralVolumeAssetPaths1728(string outputFolder, out string neutralCaveSdfPath, out string neutralAbyssalFlowPath, out string failure)
        {
            neutralCaveSdfPath = string.Empty;
            neutralAbyssalFlowPath = string.Empty;
            if (!TryEnsureAssetFolder(outputFolder, out string normalizedOutputFolder, out failure))
            {
                failure = "neutral volume output folder rejected: " + failure;
                return false;
            }

            neutralCaveSdfPath = normalizedOutputFolder + "/" + NeutralCaveSdfAssetName1728;
            neutralAbyssalFlowPath = normalizedOutputFolder + "/" + NeutralAbyssalFlowAssetName1728;
            return true;
        }

        private static bool TryBakeNeutralVolumeTexture1728(string assetPath, Color voxel, out string failure)
        {
            failure = string.Empty;
            Texture3D texture = null;
            try
            {
                texture = new Texture3D(1, 1, 1, TextureFormat.RGBA32, false)
                {
                    name = Path.GetFileNameWithoutExtension(assetPath),
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Point,
                    anisoLevel = 0
                };
                texture.SetPixel(0, 0, 0, voxel);
                texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);

                Texture3D existing = AssetDatabase.LoadAssetAtPath<Texture3D>(assetPath);
                if (existing == null)
                {
                    AssetDatabase.CreateAsset(texture, assetPath);
                    texture = null;
                }
                else
                {
                    EditorUtility.CopySerialized(texture, existing);
                    existing.name = Path.GetFileNameWithoutExtension(assetPath);
                    EditorUtility.SetDirty(existing);
                }

                return true;
            }
            catch (Exception ex) when (ex is UnityException || ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException || ex is ArgumentException || ex is NotSupportedException)
            {
                failure = "neutral marine-snow volume bake failed for " + assetPath + ": " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
            finally
            {
                if (texture != null)
                    UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
