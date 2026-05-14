using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;

namespace Hecton8.Core
{
    /// <summary>
    /// Cached Android/Vulkan/XR policy for Quest-class TBDR paths.
    /// </summary>
    public static class QuestVulkanRuntimePolicy
    {
        public const int QuestMemoryGateMegabytes = 8000;

        private const int QuestFamilyMemoryCeilingMegabytes = 9000;

        private static bool _initialized;
        private static bool _isAndroid;
        private static bool _isVulkan;
        private static bool _questMemoryGate;
        private static bool _questFamilyMemoryGate;
        private static bool _questDeviceSignature;
        private static int _systemMemoryMegabytes;

        public static int SystemMemoryMegabytes
        {
            get
            {
                EnsureInitialized();
                return _systemMemoryMegabytes;
            }
        }

        public static bool IsQuestMemoryGate
        {
            get
            {
                EnsureInitialized();
                return _questMemoryGate;
            }
        }

        public static bool IsQuestVulkanCandidate
        {
            get
            {
                EnsureInitialized();
                return _isAndroid && _isVulkan && (_questMemoryGate || _questFamilyMemoryGate || _questDeviceSignature);
            }
        }

        public static bool IsQuestRuntimeActive
        {
            get
            {
                EnsureInitialized();
                return IsQuestVulkanCandidate && (HectonXRRuntimeState.IsXRActive || XRSettings.enabled || XRSettings.isDeviceActive);
            }
        }

        public static bool UseDepthlessTBDRPath => IsQuestRuntimeActive;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _initialized = false;
            _isAndroid = false;
            _isVulkan = false;
            _questMemoryGate = false;
            _questFamilyMemoryGate = false;
            _questDeviceSignature = false;
            _systemMemoryMegabytes = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeBeforeSceneLoad()
        {
            EnsureInitialized();
        }

        public static void EnsureInitialized()
        {
            if (_initialized)
                return;

            _systemMemoryMegabytes = Mathf.Max(0, SystemInfo.systemMemorySize);
            _isAndroid = Application.platform == RuntimePlatform.Android;
            _isVulkan = SystemInfo.graphicsDeviceType == GraphicsDeviceType.Vulkan;
            _questMemoryGate = _systemMemoryMegabytes > 0 && _systemMemoryMegabytes < QuestMemoryGateMegabytes;
            _questFamilyMemoryGate = _systemMemoryMegabytes > 0 && _systemMemoryMegabytes < QuestFamilyMemoryCeilingMegabytes;
            _questDeviceSignature =
                ContainsQuestToken(SystemInfo.deviceModel) ||
                ContainsQuestToken(SystemInfo.deviceName) ||
                ContainsQuestToken(XRSettings.loadedDeviceName);
            _initialized = true;
        }

        private static bool ContainsQuestToken(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            return value.IndexOf("Quest", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("Oculus", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("Meta", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    /// <summary>
    /// Applies Quest-class fixed foveated rendering and texture residency clamps without touching PC paths.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9820)]
    [AddComponentMenu("Hecton8/Rendering/Oculus FFR Enforcer")]
    public sealed class OculusFfrEnforcer : MonoBehaviour, IUpdatable, IGlobalRegistryHotSwapListener
    {
        private const int BlackboxCapacity = 300;
        private const int DefaultSampleIntervalFrames = 60;
        private const int HalfResolutionMipLimit = 1;
        private const float DefaultTargetFoveationLevel = 0.85f;
        private const uint BlackboxMagic = 0x51464652u; // QFFR
        private const uint BlackboxVersion = 1u;
        private const ushort FlagQuestRuntime = 1 << 0;
        private const ushort FlagMemoryUnderEightGb = 1 << 1;
        private const ushort FlagFfrApplied = 1 << 2;
        private const ushort FlagCapsSupported = 1 << 3;
        private const ushort FlagMipLimitApplied = 1 << 4;
        private const ushort FlagNonFinite = 1 << 5;
        private const string DumpFileName = "Dump_QUEST_VULKAN_RENDER_PIPELINE.bin";

        private static readonly List<XRDisplaySubsystem> DisplaySubsystems = new List<XRDisplaySubsystem>(4);

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Unity XRDisplaySubsystem foveation intensity. 0.85 maps to High/HighTop on Quest-class runtimes.")]
        private float targetFoveationLevel = DefaultTargetFoveationLevel;

        [SerializeField, Range(1, 240)]
        [Tooltip("Frames between FFR/eye texture telemetry samples.")]
        private int sampleIntervalFrames = DefaultSampleIntervalFrames;

        [SerializeField]
        [Tooltip("Quest-only texture clamp. Uses masterTextureLimit/globalTextureMipmapLimit >= 1.")]
        private bool enforceHalfResolutionTextures = true;

        private NativeArray<QuestFfrBlackboxEntry> _blackbox;
        private int _blackboxWriteIndex;
        private int _framesUntilSample;
        private bool _registeredUpdate;
        private bool _registeredHotSwap;
        private bool _subscribedToXRState;
        private bool _allocatedBlackbox;
        private bool _capturedMipLimit;
        private bool _changedMipLimit;
        private int _baselineMasterTextureLimit;
        private int _baselineGlobalTextureMipmapLimit;
        private float _lastAppliedLevel = -1f;

        private void Awake()
        {
            EnsureBlackbox();
            _framesUntilSample = 0;
        }

        private void OnEnable()
        {
            EnsureBlackbox();
            SubscribeXRState();
            TryRegisterHotSwapListener();
            TryRegisterUpdate();
            ApplyQuestPolicy(force: true);
        }

        private void Start()
        {
            TryRegisterHotSwapListener();
            TryRegisterUpdate();
            ApplyQuestPolicy(force: true);
        }

        private void OnDisable()
        {
            if (_registeredUpdate)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
                _registeredUpdate = false;
            }

            TryUnregisterHotSwapListener();
            UnsubscribeXRState();
            RestoreEditorTextureLimit();
        }

        private void OnDestroy()
        {
            if (_allocatedBlackbox)
            {
                _blackbox.Dispose();
                _allocatedBlackbox = false;
            }
        }

        public void Tick(float deltaTime)
        {
            _framesUntilSample--;
            if (_framesUntilSample > 0)
                return;

            _framesUntilSample = Mathf.Max(1, sampleIntervalFrames);
            ApplyQuestPolicy(force: false);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            if (currentService == null)
            {
                _registeredUpdate = false;
                return;
            }

            TryRegisterUpdate();
        }

        public void RequestBlackboxDump()
        {
            DumpBlackbox();
        }

        private void TryRegisterUpdate()
        {
            if (_registeredUpdate || !Application.isPlaying)
                return;

            _registeredUpdate = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Core);
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwap || !Application.isPlaying)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        private void SubscribeXRState()
        {
            if (_subscribedToXRState)
                return;

            HectonXRRuntimeState.XRActiveChanged += HandleXRActiveChanged;
            _subscribedToXRState = true;
        }

        private void UnsubscribeXRState()
        {
            if (!_subscribedToXRState)
                return;

            HectonXRRuntimeState.XRActiveChanged -= HandleXRActiveChanged;
            _subscribedToXRState = false;
        }

        private void HandleXRActiveChanged(bool active)
        {
            _framesUntilSample = 0;
            ApplyQuestPolicy(force: true);
        }

        private void ApplyQuestPolicy(bool force)
        {
            QuestVulkanRuntimePolicy.EnsureInitialized();
            RenderTextureDescriptor eyeDescriptor = HectonXRManager.RefreshEyeDescriptor();
            bool questRuntime = QuestVulkanRuntimePolicy.IsQuestRuntimeActive;
            bool capsSupported = SystemInfo.foveatedRenderingCaps != FoveatedRenderingCaps.None;
            bool ffrApplied = false;
            float appliedLevel = 0f;

            if (questRuntime)
            {
                if (enforceHalfResolutionTextures)
                    ApplyQuestTextureLimit();

                ffrApplied = TryApplyFoveation(force, capsSupported, out appliedLevel);
            }

            if (!ffrApplied)
                _lastAppliedLevel = -1f;

            HectonXRRuntimeState.ReportHardwareFoveationState(
                ffrApplied,
                appliedLevel,
                eyeDescriptor.width,
                eyeDescriptor.height);

            ushort flags = 0;
            if (questRuntime)
                flags |= FlagQuestRuntime;
            if (QuestVulkanRuntimePolicy.IsQuestMemoryGate)
                flags |= FlagMemoryUnderEightGb;
            if (ffrApplied)
                flags |= FlagFfrApplied;
            if (capsSupported)
                flags |= FlagCapsSupported;
            if (_changedMipLimit)
                flags |= FlagMipLimitApplied;
            if (!IsFinite(appliedLevel) || eyeDescriptor.width <= 0 || eyeDescriptor.height <= 0)
                flags |= FlagNonFinite;

            WriteBlackbox(flags, appliedLevel, eyeDescriptor.width, eyeDescriptor.height);
            if ((flags & FlagNonFinite) != 0)
                DumpBlackbox();
        }

        private bool TryApplyFoveation(bool force, bool capsSupported, out float appliedLevel)
        {
            appliedLevel = 0f;
            if (!capsSupported && !XRSettings.enabled)
                return false;

            float targetLevel = Mathf.Clamp01(targetFoveationLevel);
            if (!force && Mathf.Abs(_lastAppliedLevel - targetLevel) <= 0.0001f)
            {
                appliedLevel = _lastAppliedLevel;
                return _lastAppliedLevel > 0f;
            }

            bool applied = false;
            DisplaySubsystems.Clear();
            SubsystemManager.GetSubsystems(DisplaySubsystems);
            for (int i = 0; i < DisplaySubsystems.Count; i++)
            {
                XRDisplaySubsystem display = DisplaySubsystems[i];
                if (display == null || !display.running)
                    continue;

                display.foveatedRenderingFlags = XRDisplaySubsystem.FoveatedRenderingFlags.None;
                display.foveatedRenderingLevel = targetLevel;
                appliedLevel = Mathf.Max(appliedLevel, display.foveatedRenderingLevel);
                applied = true;
            }

            _lastAppliedLevel = applied ? appliedLevel : -1f;
            return applied && appliedLevel > 0f;
        }

        private void ApplyQuestTextureLimit()
        {
            if (!_capturedMipLimit)
            {
#pragma warning disable 0618
                _baselineMasterTextureLimit = QualitySettings.masterTextureLimit;
#pragma warning restore 0618
                _baselineGlobalTextureMipmapLimit = QualitySettings.globalTextureMipmapLimit;
                _capturedMipLimit = true;
            }

#pragma warning disable 0618
            int masterLimit = Mathf.Max(QualitySettings.masterTextureLimit, HalfResolutionMipLimit);
            if (QualitySettings.masterTextureLimit != masterLimit)
            {
                QualitySettings.masterTextureLimit = masterLimit;
                _changedMipLimit = true;
            }
#pragma warning restore 0618

            int globalLimit = Mathf.Max(QualitySettings.globalTextureMipmapLimit, HalfResolutionMipLimit);
            if (QualitySettings.globalTextureMipmapLimit != globalLimit)
            {
                QualitySettings.globalTextureMipmapLimit = globalLimit;
                _changedMipLimit = true;
            }
        }

        private void RestoreEditorTextureLimit()
        {
#if UNITY_EDITOR
            if (!Application.isEditor || Application.isBatchMode || !_capturedMipLimit || !_changedMipLimit)
                return;

#pragma warning disable 0618
            QualitySettings.masterTextureLimit = _baselineMasterTextureLimit;
#pragma warning restore 0618
            QualitySettings.globalTextureMipmapLimit = _baselineGlobalTextureMipmapLimit;
#endif
        }

        private void EnsureBlackbox()
        {
            if (_allocatedBlackbox)
                return;

            _blackbox = new NativeArray<QuestFfrBlackboxEntry>(
                BlackboxCapacity,
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<QuestFfrBlackboxEntry>[300] - Quest FFR blackbox ring - owner: OculusFfrEnforcer
            _allocatedBlackbox = true;
        }

        private void WriteBlackbox(ushort flags, float appliedLevel, int eyeWidth, int eyeHeight)
        {
            if (!_allocatedBlackbox)
                return;

            int slot = _blackboxWriteIndex;
            _blackboxWriteIndex++;
            if (_blackboxWriteIndex >= BlackboxCapacity)
                _blackboxWriteIndex = 0;

            _blackbox[slot] = new QuestFfrBlackboxEntry
            {
                Frame = Time.frameCount,
                SystemMemoryMb = QuestVulkanRuntimePolicy.SystemMemoryMegabytes,
                EyeWidth = eyeWidth,
                EyeHeight = eyeHeight,
                FfrLevelQ8 = (ushort)Mathf.Clamp(Mathf.RoundToInt(appliedLevel * 255f), 0, 255),
                Flags = flags,
            };
        }

        private void DumpBlackbox()
        {
            if (!_allocatedBlackbox)
                return;

            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs", "AgentLogs"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, DumpFileName);

            using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(BlackboxMagic);
            writer.Write(BlackboxVersion);
            writer.Write(BlackboxCapacity);
            writer.Write(Marshal.SizeOf<QuestFfrBlackboxEntry>());
            writer.Write(_blackboxWriteIndex);
            for (int i = 0; i < BlackboxCapacity; i++)
            {
                QuestFfrBlackboxEntry entry = _blackbox[i];
                writer.Write(entry.Frame);
                writer.Write(entry.SystemMemoryMb);
                writer.Write(entry.EyeWidth);
                writer.Write(entry.EyeHeight);
                writer.Write(entry.FfrLevelQ8);
                writer.Write(entry.Flags);
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct QuestFfrBlackboxEntry
        {
            public int Frame;
            public int SystemMemoryMb;
            public int EyeWidth;
            public int EyeHeight;
            public ushort FfrLevelQ8;
            public ushort Flags;
        }
    }
}
