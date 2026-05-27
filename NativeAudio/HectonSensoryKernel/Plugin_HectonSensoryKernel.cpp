#include "AudioPluginInterface.h"
#include <stdio.h>
#include <stddef.h>
#include <string.h>
#if PLATFORM_WIN
#include <stdint.h>
#include <windows.h>
#else
#include <sched.h>
#include <sys/stat.h>
#include <sys/types.h>
#endif

namespace HectonSensoryKernel
{
    enum BridgeStatusBits
    {
        kStatusNone = 0,
        kStatusActive = 1 << 0,
        kStatusDescriptorMagicMismatch = 1 << 1,
        kStatusNullPointer = 1 << 2,
        kStatusAlignmentInvalid = 1 << 3,
        kStatusCapacityInvalid = 1 << 4,
        kStatusSharedStateInvalid = 1 << 5,
        kStatusCleared = 1 << 6,
        kStatusBusy = 1 << 7
    };

    enum SharedStateSlots
    {
        kReadIndexSlot = 0,
        kWriteIndexSlot = 2,
        kCapacityFramesSlot = 4,
        kCapacityMaskSlot = 6,
        kGuardValueSlotA = 8,
        kGuardValueSlotB = 10,
        kSourceChannelsSlot = 12,
        kSharedStateSlotCount = 14
    };

    static const UInt32 kDescriptorMagic = 0x484B3031u;
    static const size_t kRequiredPointerAlignmentBytes = 8u;
    static const char* kTelemetryDumpPath = "Docs/AgentLogs/Dump_1314_AudioBridge.bin";
    static const SInt32 kTelemetryDumpMaxBytes = 16 + 300 * 64;
    static const SInt32 kSharedStateGuardValueA = (SInt32)0x48454354;
    static const SInt32 kSharedStateGuardValueB = (SInt32)0x4F4E2D38;
    static const int kDrainSpinLimit = 1000000;
    static const unsigned int kMaxProcessFrames = 65536u;
    static const int kMaxProcessChannels = 64;
    static const size_t kMaxProcessOutputSamples = (size_t)kMaxProcessFrames * (size_t)kMaxProcessChannels;

#if PLATFORM_WIN
    typedef LONG HectonAtomicInt32;
#else
    typedef SInt32 HectonAtomicInt32;
#endif

    static_assert(sizeof(HectonAtomicInt32) == sizeof(SInt32), "Hecton native atomic cursor storage must stay 32-bit.");

    static inline SInt32 AtomicRead32(volatile HectonAtomicInt32* value)
    {
        if (value == NULL)
            return 0;

#if PLATFORM_WIN
        return (SInt32)InterlockedCompareExchange(value, 0, 0);
#else
        return (SInt32)__sync_val_compare_and_swap(value, 0, 0);
#endif
    }

    static inline void AtomicWrite32(volatile HectonAtomicInt32* value, SInt32 newValue)
    {
        if (value == NULL)
            return;

#if PLATFORM_WIN
        InterlockedExchange(value, (LONG)newValue);
#else
        __sync_lock_test_and_set(value, newValue);
        __sync_synchronize();
#endif
    }

    static inline SInt32 AtomicIncrement32(volatile HectonAtomicInt32* value)
    {
        if (value == NULL)
            return 0;

#if PLATFORM_WIN
        return (SInt32)InterlockedIncrement(value);
#else
        return (SInt32)__sync_add_and_fetch(value, 1);
#endif
    }

    static inline SInt32 AtomicDecrement32(volatile HectonAtomicInt32* value)
    {
        if (value == NULL)
            return 0;

#if PLATFORM_WIN
        return (SInt32)InterlockedDecrement(value);
#else
        return (SInt32)__sync_sub_and_fetch(value, 1);
#endif
    }

    static inline void EnsureTelemetryDumpDirectory()
    {
#if PLATFORM_WIN
        CreateDirectoryA("Docs", NULL);
        CreateDirectoryA("Docs/AgentLogs", NULL);
#else
        mkdir("Docs", 0755);
        mkdir("Docs/AgentLogs", 0755);
#endif
    }

    static inline int WriteTelemetryDumpFile(const void* bytes, int byteCount)
    {
        if (bytes == NULL || byteCount <= 0 || byteCount > kTelemetryDumpMaxBytes)
            return 0;

        EnsureTelemetryDumpDirectory();

#if PLATFORM_WIN
        FILE* file = NULL;
        if (fopen_s(&file, kTelemetryDumpPath, "wb") != 0 || file == NULL)
            return 0;
#else
        FILE* file = fopen(kTelemetryDumpPath, "wb");
        if (file == NULL)
            return 0;
#endif

        const size_t bytesToWrite = (size_t)byteCount;
        const size_t written = fwrite(bytes, 1u, bytesToWrite, file);
        fclose(file);
        return written == bytesToWrite ? 1 : 0;
    }

    struct SharedRingBufferDescriptor
    {
        float* frames;
        volatile SInt32* sharedState;
        volatile SInt32* readIndex;
        volatile SInt32* writeIndex;
        UInt32 descriptorMagic;
        SInt32 capacityFrames;
        SInt32 capacityMask;
        SInt32 sharedStateLengthInts;
        SInt32 sourceChannels;
        SInt32 reserved0;
    };

    static_assert(sizeof(void*) == 8u, "Hecton shared ring descriptor requires a 64-bit native plugin ABI.");
    static_assert(sizeof(SharedRingBufferDescriptor) == 56u, "SharedRingBufferDescriptor size must match C# NativeAudioKernelRingBufferDescriptor.");
    static_assert(offsetof(SharedRingBufferDescriptor, frames) == 0u, "frames offset mismatch.");
    static_assert(offsetof(SharedRingBufferDescriptor, sharedState) == 8u, "sharedState offset mismatch.");
    static_assert(offsetof(SharedRingBufferDescriptor, readIndex) == 16u, "readIndex offset mismatch.");
    static_assert(offsetof(SharedRingBufferDescriptor, writeIndex) == 24u, "writeIndex offset mismatch.");
    static_assert(offsetof(SharedRingBufferDescriptor, descriptorMagic) == 32u, "descriptorMagic offset mismatch.");
    static_assert(offsetof(SharedRingBufferDescriptor, capacityFrames) == 36u, "capacityFrames offset mismatch.");
    static_assert(offsetof(SharedRingBufferDescriptor, capacityMask) == 40u, "capacityMask offset mismatch.");
    static_assert(offsetof(SharedRingBufferDescriptor, sharedStateLengthInts) == 44u, "sharedStateLengthInts offset mismatch.");
    static_assert(offsetof(SharedRingBufferDescriptor, sourceChannels) == 48u, "sourceChannels offset mismatch.");
    static_assert(offsetof(SharedRingBufferDescriptor, reserved0) == 52u, "reserved0 offset mismatch.");

    static SharedRingBufferDescriptor g_sharedRingBuffer = {};
    static volatile HectonAtomicInt32 g_hasSharedRingBuffer = 0;
    static volatile HectonAtomicInt32 g_callbackMutationGate = 0;
    static volatile HectonAtomicInt32 g_processCallbackDepth = 0;
    static volatile HectonAtomicInt32 g_lastStatusBits = kStatusCleared;
    static volatile HectonAtomicInt32 g_debugProcessScratchInUse = 0;
    static float g_debugProcessScratch[4096 * 8] = {};

    static inline int ReadSharedIndex(volatile SInt32* value)
    {
        return (int)AtomicRead32((volatile HectonAtomicInt32*)value);
    }

    static inline void WriteSharedIndex(volatile SInt32* value, int newValue)
    {
        AtomicWrite32((volatile HectonAtomicInt32*)value, (SInt32)newValue);
    }

    static inline SInt32 ReadStatusBits()
    {
        return AtomicRead32(&g_lastStatusBits);
    }

    static inline void WriteStatusBits(SInt32 value)
    {
        AtomicWrite32(&g_lastStatusBits, value);
    }

    static inline bool IsAlignedPointer(const volatile void* pointer, size_t alignment)
    {
        if (pointer == NULL || alignment == 0)
            return false;

        return (((size_t)pointer) & (alignment - 1)) == 0;
    }

    static inline bool IsPowerOfTwo(int value)
    {
        return value > 0 && (value & (value - 1)) == 0;
    }

    static inline void ClearSharedRingBufferUnsafe()
    {
        memset(&g_sharedRingBuffer, 0, sizeof(g_sharedRingBuffer));
    }

    static inline bool WaitForProcessCallbacksToDrain()
    {
        for (int spinIndex = 0; spinIndex < kDrainSpinLimit; ++spinIndex)
        {
            if (AtomicRead32(&g_processCallbackDepth) == 0)
                return true;

#if PLATFORM_WIN
            Sleep(0);
#else
            sched_yield();
#endif
        }

        return AtomicRead32(&g_processCallbackDepth) == 0;
    }

    static inline void RestoreStatusAfterDrainFailure()
    {
        AtomicWrite32(&g_callbackMutationGate, 0);
        const SInt32 restoredStatus = AtomicRead32(&g_hasSharedRingBuffer) != 0 ? kStatusActive : kStatusCleared;
        WriteStatusBits(restoredStatus | kStatusBusy);
    }

    static SInt32 ValidateDescriptor(const SharedRingBufferDescriptor& descriptor)
    {
        SInt32 status = kStatusNone;

        if (descriptor.descriptorMagic != kDescriptorMagic)
            status |= kStatusDescriptorMagicMismatch;

        if (descriptor.frames == NULL ||
            descriptor.sharedState == NULL ||
            descriptor.readIndex == NULL ||
            descriptor.writeIndex == NULL)
        {
            status |= kStatusNullPointer;
        }

        if (!IsAlignedPointer(descriptor.frames, kRequiredPointerAlignmentBytes) ||
            !IsAlignedPointer(descriptor.sharedState, kRequiredPointerAlignmentBytes) ||
            !IsAlignedPointer(descriptor.readIndex, kRequiredPointerAlignmentBytes) ||
            !IsAlignedPointer(descriptor.writeIndex, kRequiredPointerAlignmentBytes))
        {
            status |= kStatusAlignmentInvalid;
        }

        if (descriptor.capacityFrames <= 1 ||
            descriptor.capacityFrames > (SInt32)kMaxProcessFrames ||
            !IsPowerOfTwo(descriptor.capacityFrames) ||
            descriptor.capacityMask != descriptor.capacityFrames - 1)
        {
            status |= kStatusCapacityInvalid;
        }

        if (descriptor.sourceChannels < 1 ||
            descriptor.sourceChannels > 2)
        {
            status |= kStatusSharedStateInvalid;
        }

        if (descriptor.sharedStateLengthInts < kSharedStateSlotCount)
            status |= kStatusSharedStateInvalid;

        if (status != kStatusNone)
            return status;

        if (descriptor.readIndex != descriptor.sharedState + kReadIndexSlot ||
            descriptor.writeIndex != descriptor.sharedState + kWriteIndexSlot)
        {
            status |= kStatusSharedStateInvalid;
        }

        const int sharedCapacityFrames = ReadSharedIndex(descriptor.sharedState + kCapacityFramesSlot);
        const int sharedCapacityMask = ReadSharedIndex(descriptor.sharedState + kCapacityMaskSlot);
        const int guardValueA = ReadSharedIndex(descriptor.sharedState + kGuardValueSlotA);
        const int guardValueB = ReadSharedIndex(descriptor.sharedState + kGuardValueSlotB);
        const int sourceChannels = ReadSharedIndex(descriptor.sharedState + kSourceChannelsSlot);
        if (sharedCapacityFrames != descriptor.capacityFrames ||
            sharedCapacityMask != descriptor.capacityMask ||
            guardValueA != kSharedStateGuardValueA ||
            guardValueB != kSharedStateGuardValueB ||
            sourceChannels != descriptor.sourceChannels)
        {
            status |= kStatusSharedStateInvalid;
        }

        return status;
    }

    static inline bool TryComputeOutputSampleCount(unsigned int frameCount, int channelCount, size_t* sampleCount)
    {
        if (sampleCount == NULL)
            return false;

        *sampleCount = 0u;
        if (frameCount == 0u || channelCount <= 0)
            return false;

        const size_t frames = (size_t)frameCount;
        const size_t channels = (size_t)channelCount;
        if (frames > ((size_t)-1) / channels)
            return false;

        const size_t totalSamples = frames * channels;
        if (totalSamples > kMaxProcessOutputSamples)
            return false;

        *sampleCount = totalSamples;
        return true;
    }

    int InternalRegisterEffectDefinition(UnityAudioEffectDefinition& definition)
    {
        definition.flags = 0;
        definition.paramdefs = NULL;
        definition.numparameters = 0;
        return 0;
    }

    UNITY_AUDIODSP_RESULT UNITY_AUDIODSP_CALLBACK CreateCallback(UnityAudioEffectState* state)
    {
        if (state != NULL)
            state->effectdata = NULL;

        return UNITY_AUDIODSP_OK;
    }

    UNITY_AUDIODSP_RESULT UNITY_AUDIODSP_CALLBACK ReleaseCallback(UnityAudioEffectState* state)
    {
        return UNITY_AUDIODSP_OK;
    }

    UNITY_AUDIODSP_RESULT UNITY_AUDIODSP_CALLBACK SetFloatParameterCallback(UnityAudioEffectState* state, int index, float value)
    {
        return UNITY_AUDIODSP_ERR_UNSUPPORTED;
    }

    UNITY_AUDIODSP_RESULT UNITY_AUDIODSP_CALLBACK GetFloatParameterCallback(UnityAudioEffectState* state, int index, float* value, char* valuestr)
    {
        if (value != NULL)
            *value = 0.0f;
        if (valuestr != NULL)
            valuestr[0] = 0;
        return UNITY_AUDIODSP_ERR_UNSUPPORTED;
    }

    UNITY_AUDIODSP_RESULT UNITY_AUDIODSP_CALLBACK GetFloatBufferCallback(UnityAudioEffectState* state, const char* name, float* buffer, int numsamples)
    {
        return UNITY_AUDIODSP_OK;
    }

    UNITY_AUDIODSP_RESULT UNITY_AUDIODSP_CALLBACK ProcessCallback(
        UnityAudioEffectState* state,
        float* inbuffer,
        float* outbuffer,
        unsigned int length,
        int inchannels,
        int outchannels)
    {
        if (outbuffer == NULL)
            return UNITY_AUDIODSP_OK;

        if (length == 0u ||
            length > kMaxProcessFrames ||
            outchannels <= 0 ||
            outchannels > kMaxProcessChannels)
        {
            WriteStatusBits(kStatusSharedStateInvalid);
            return UNITY_AUDIODSP_OK;
        }

        size_t outputSampleCount = 0u;
        if (!TryComputeOutputSampleCount(length, outchannels, &outputSampleCount))
        {
            WriteStatusBits(kStatusSharedStateInvalid);
            return UNITY_AUDIODSP_OK;
        }

        memset(outbuffer, 0, sizeof(float) * outputSampleCount);

        if (inchannels > kMaxProcessChannels || (inbuffer != NULL && inchannels <= 0))
        {
            WriteStatusBits(kStatusSharedStateInvalid);
            return UNITY_AUDIODSP_OK;
        }

        const int frameCount = (int)length;
        if (inbuffer != NULL && inchannels > 0 && inchannels <= kMaxProcessChannels)
        {
            const int passthroughChannels = (inchannels < outchannels) ? inchannels : outchannels;
            for (int frameIndex = 0; frameIndex < frameCount; ++frameIndex)
            {
                const size_t inputBase = (size_t)frameIndex * (size_t)inchannels;
                const size_t outputBase = (size_t)frameIndex * (size_t)outchannels;
                for (int channelIndex = 0; channelIndex < passthroughChannels; ++channelIndex)
                    outbuffer[outputBase + channelIndex] = inbuffer[inputBase + channelIndex];

                for (int channelIndex = passthroughChannels; channelIndex < outchannels; ++channelIndex)
                    outbuffer[outputBase + channelIndex] = inbuffer[inputBase];
            }
        }

        AtomicIncrement32(&g_processCallbackDepth);
        if (AtomicRead32(&g_callbackMutationGate) != 0 ||
            AtomicRead32(&g_hasSharedRingBuffer) == 0)
        {
            AtomicDecrement32(&g_processCallbackDepth);
            return UNITY_AUDIODSP_OK;
        }

        SharedRingBufferDescriptor ringBuffer = g_sharedRingBuffer;
        const SInt32 validationStatus = ValidateDescriptor(ringBuffer);
        if (validationStatus != kStatusNone)
        {
            AtomicWrite32(&g_hasSharedRingBuffer, 0);
            WriteStatusBits(validationStatus);
            AtomicDecrement32(&g_processCallbackDepth);
            return UNITY_AUDIODSP_OK;
        }

        const int rawReadIndex = ReadSharedIndex(ringBuffer.readIndex);
        const int rawWriteIndex = ReadSharedIndex(ringBuffer.writeIndex);
        if (rawReadIndex < 0 ||
            rawReadIndex >= ringBuffer.capacityFrames ||
            rawWriteIndex < 0 ||
            rawWriteIndex >= ringBuffer.capacityFrames)
        {
            AtomicWrite32(&g_hasSharedRingBuffer, 0);
            WriteStatusBits(kStatusSharedStateInvalid);
            AtomicDecrement32(&g_processCallbackDepth);
            return UNITY_AUDIODSP_OK;
        }

        int readIndex = rawReadIndex;
        const int writeIndex = rawWriteIndex;
        const int availableFrames = (writeIndex - readIndex) & ringBuffer.capacityMask;
        const int readableFrames = (availableFrames < frameCount) ? availableFrames : frameCount;
        const int sourceChannels = ringBuffer.sourceChannels;

        for (int frameIndex = 0; frameIndex < readableFrames; ++frameIndex)
        {
            const int sourceFrameIndex = (readIndex + frameIndex) & ringBuffer.capacityMask;
            const size_t outputBase = (size_t)frameIndex * (size_t)outchannels;
            if (sourceChannels == 2)
            {
                const int sourceBase = sourceFrameIndex << 1;
                const float left = ringBuffer.frames[sourceBase];
                const float right = ringBuffer.frames[sourceBase + 1];
                if (outchannels == 1)
                {
                    outbuffer[outputBase] += (left + right) * 0.5f;
                }
                else
                {
                    outbuffer[outputBase] += left;
                    outbuffer[outputBase + 1] += right;
                    for (int channelIndex = 2; channelIndex < outchannels; ++channelIndex)
                        outbuffer[outputBase + channelIndex] += (left + right) * 0.5f;
                }
            }
            else
            {
                const float sample = ringBuffer.frames[sourceFrameIndex];
                for (int channelIndex = 0; channelIndex < outchannels; ++channelIndex)
                    outbuffer[outputBase + channelIndex] += sample;
            }
        }

        if (readableFrames > 0)
        {
            readIndex = (readIndex + readableFrames) & ringBuffer.capacityMask;
            WriteSharedIndex(ringBuffer.readIndex, readIndex);
        }

        WriteStatusBits(kStatusActive);
        AtomicDecrement32(&g_processCallbackDepth);
        return UNITY_AUDIODSP_OK;
    }

    static inline void CopyEffectName(char* target, size_t targetBytes)
    {
        if (target == NULL || targetBytes == 0u)
            return;

        static const char kEffectName[] = "Hecton Sensory Kernel";
        size_t index = 0u;
        const size_t lastIndex = targetBytes - 1u;
        while (index < lastIndex && kEffectName[index] != 0)
        {
            target[index] = kEffectName[index];
            ++index;
        }

        target[index] = 0;
    }

    static inline void FillUnityEffectDefinition(UnityAudioEffectDefinition& definition)
    {
        memset(&definition, 0, sizeof(definition));
        CopyEffectName(definition.name, sizeof(definition.name));
        definition.structsize = sizeof(UnityAudioEffectDefinition);
        definition.paramstructsize = sizeof(UnityAudioParameterDefinition);
        definition.apiversion = UNITY_AUDIO_PLUGIN_API_VERSION;
        definition.pluginversion = 0x010000;
        definition.create = CreateCallback;
        definition.release = ReleaseCallback;
        definition.process = ProcessCallback;
        definition.setfloatparameter = SetFloatParameterCallback;
        definition.getfloatparameter = GetFloatParameterCallback;
        definition.getfloatbuffer = GetFloatBufferCallback;
        InternalRegisterEffectDefinition(definition);
    }

    extern "C" UNITY_AUDIODSP_EXPORT_API int AUDIO_CALLING_CONVENTION UnityGetAudioEffectDefinitions(UnityAudioEffectDefinition*** definitionptr)
    {
        static UnityAudioEffectDefinition definition;
        static UnityAudioEffectDefinition* definitionPointers[1];
        if (definitionptr == NULL)
            return 0;

        FillUnityEffectDefinition(definition);
        definitionPointers[0] = &definition;
        *definitionptr = definitionPointers;
        return 1;
    }

    static SInt32 RegisterSharedRingBufferOperation(const SharedRingBufferDescriptor* descriptor)
    {
        AtomicWrite32(&g_callbackMutationGate, 1);
        WriteStatusBits(kStatusBusy);
        if (!WaitForProcessCallbacksToDrain())
        {
            RestoreStatusAfterDrainFailure();
            return ReadStatusBits();
        }

        if (descriptor == NULL)
        {
            AtomicWrite32(&g_hasSharedRingBuffer, 0);
            ClearSharedRingBufferUnsafe();
            WriteStatusBits(kStatusCleared);
            AtomicWrite32(&g_callbackMutationGate, 0);
            return kStatusCleared;
        }

        const SInt32 validationStatus = ValidateDescriptor(*descriptor);
        if (validationStatus != kStatusNone)
        {
            WriteStatusBits(validationStatus);
            AtomicWrite32(&g_callbackMutationGate, 0);
            return validationStatus;
        }

        AtomicWrite32(&g_hasSharedRingBuffer, 0);
        g_sharedRingBuffer = *descriptor;
        WriteStatusBits(kStatusActive);
        AtomicWrite32(&g_hasSharedRingBuffer, 1);
        AtomicWrite32(&g_callbackMutationGate, 0);
        return kStatusActive;
    }

    static SInt32 ClearSharedRingBufferOperation()
    {
        AtomicWrite32(&g_callbackMutationGate, 1);
        WriteStatusBits(kStatusBusy);
        if (!WaitForProcessCallbacksToDrain())
        {
            RestoreStatusAfterDrainFailure();
            return ReadStatusBits();
        }

        AtomicWrite32(&g_hasSharedRingBuffer, 0);
        ClearSharedRingBufferUnsafe();
        WriteStatusBits(kStatusCleared);
        AtomicWrite32(&g_callbackMutationGate, 0);
        return kStatusCleared;
    }

    extern "C" UNITY_AUDIODSP_EXPORT_API void AUDIO_CALLING_CONVENTION HectonSensoryKernel_RegisterSharedRingBuffer(const SharedRingBufferDescriptor* descriptor)
    {
        (void)RegisterSharedRingBufferOperation(descriptor);
    }

    extern "C" UNITY_AUDIODSP_EXPORT_API int AUDIO_CALLING_CONVENTION HectonSensoryKernel_RegisterSharedRingBufferAndGetStatus(const SharedRingBufferDescriptor* descriptor)
    {
        return (int)RegisterSharedRingBufferOperation(descriptor);
    }

    extern "C" UNITY_AUDIODSP_EXPORT_API void AUDIO_CALLING_CONVENTION HectonSensoryKernel_ClearSharedRingBuffer()
    {
        (void)ClearSharedRingBufferOperation();
    }

    extern "C" UNITY_AUDIODSP_EXPORT_API int AUDIO_CALLING_CONVENTION HectonSensoryKernel_ClearSharedRingBufferAndGetStatus()
    {
        return (int)ClearSharedRingBufferOperation();
    }

    extern "C" UNITY_AUDIODSP_EXPORT_API int AUDIO_CALLING_CONVENTION HectonSensoryKernel_GetSharedRingBufferStatus()
    {
        return (int)ReadStatusBits();
    }

    extern "C" UNITY_AUDIODSP_EXPORT_API int AUDIO_CALLING_CONVENTION HectonSensoryKernel_DumpAudioBridgeTelemetry(const void* bytes, int byteCount)
    {
        return WriteTelemetryDumpFile(bytes, byteCount);
    }

    extern "C" UNITY_AUDIODSP_EXPORT_API int AUDIO_CALLING_CONVENTION HectonSensoryKernel_DebugProcessBlock(int frameCount, int outchannels)
    {
        if (frameCount <= 0 || frameCount > 4096 || outchannels <= 0 || outchannels > 8)
            return kStatusCapacityInvalid;

        const size_t sampleCount = (size_t)frameCount * (size_t)outchannels;
        if (AtomicIncrement32(&g_debugProcessScratchInUse) != 1)
        {
            AtomicDecrement32(&g_debugProcessScratchInUse);
            return kStatusBusy;
        }

        memset(g_debugProcessScratch, 0, sizeof(float) * sampleCount);
        ProcessCallback(NULL, NULL, g_debugProcessScratch, (unsigned int)frameCount, 0, outchannels);
        const int status = (int)ReadStatusBits();
        AtomicDecrement32(&g_debugProcessScratchInUse);
        return status;
    }
}
