using Hecton8.Core;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class HectonScannedRenderTarget : MonoBehaviour
    {
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private bool startScanned;
        [SerializeField] private bool lootHighlight;
        [SerializeField] private bool environmentTarget = true;
        [SerializeField] private bool aiEntityTarget;

        private uint _baseFlags;

        private void Awake()
        {
            if (targetRenderer == null)
                targetRenderer = ComponentReferenceUtility.ResolveOwnedComponent<Renderer>(transform);

            _baseFlags = HectonScanRenderFlags.None;
            if (lootHighlight)
                _baseFlags |= HectonScanRenderFlags.Loot;
            if (environmentTarget)
                _baseFlags |= HectonScanRenderFlags.Environment;
            if (aiEntityTarget)
                _baseFlags |= HectonScanRenderFlags.AiEntity;
        }

        private void OnEnable()
        {
            if (targetRenderer == null)
                return;

            uint flags = _baseFlags;
            if (startScanned)
                flags |= HectonScanRenderFlags.IsScanned;

            HectonScanRenderRegistry.Register(targetRenderer, flags);
        }

        private void OnDisable()
        {
            if (targetRenderer != null)
                HectonScanRenderRegistry.Unregister(targetRenderer);
        }

        public void SetScanned(bool scanned)
        {
            if (targetRenderer != null)
                HectonScanRenderRegistry.SetFlags(targetRenderer, HectonScanRenderFlags.IsScanned, scanned);
        }

        public void SetLootHighlight(bool highlighted)
        {
            lootHighlight = highlighted;
            if (targetRenderer != null)
                HectonScanRenderRegistry.SetFlags(targetRenderer, HectonScanRenderFlags.Loot, highlighted);
        }
    }
}
