#include "AudioPluginUtil.h"

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
        kWriteIndexSlot = 1,
        kCapacityFramesSlot = 2,
        kCapacityMaskSlot = 3,
        kGuardValueSlotA = 4,
        kGuardValueSlotB = 5,
        kSharedStateSlotCount = 6
    };

    static const UInt32 kDescriptorMagic = 0x484B3031u;
    static const SInt32 kSharedStateGuardValueA = (SInt32)0x48454354;
    static const SInt32 kSharedStateGuardValueB = (SInt32)0x4F4E2D38;

    struct SharedRingBufferDescriptor
    {
        UInt32 descriptorMagic;
        float* frames;
        volatile SInt32* sharedState;
        volatile SInt32* readIndex;
        volatile SInt32* writeIndex;
        SInt32 capacityFrames;
        SInt32 capacityMask;
        SInt32 sharedStateLengthInts;
    };

    struct EffectData
    {
        int unused;
    };

    static SharedRingBufferDescriptor g_sharedRingBuffer = {};
    static volatile LONG g_hasSharedRingBuffer = 0;
    static volatile LONG g_processCallbackDepth = 0;
    static volatile LONG g_lastStatusBits = kStatusCleared;

    static inline int ReadSharedIndex(volatile SInt32* value)
    {
#if PLATFORM_WIN
        return (int)InterlockedCompareExchange((volatile LONG*)value, 0, 0);
#else
        return value != NULL ? *value : 0;
#endif
    }

    static inline void WriteSharedIndex(volatile SInt32* value, int newValue)
    {
#if PLATFORM_WIN
        InterlockedExchange((volatile LONG*)value, (LONG)newValue);
#else
        if (value != NULL)
            *value = newValue;
#endif
    }

    static inline LONG ReadStatusBits()
    {
#if PLATFORM_WIN
        return InterlockedCompareExchange(&g_lastStatusBits, 0, 0);
#else
        return g_lastStatusBits;
#endif
    }

    static inline void WriteStatusBits(LONG value)
    {
#if PLATFORM_WIN
        InterlockedExchange(&g_lastStatusBits, value);
#else
        g_lastStatusBits = value;
#endif
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

    static inline void WaitForProcessCallbacksToDrain()
    {
#if PLATFORM_WIN
        while (InterlockedCompareExchange(&g_processCallbackDepth, 0, 0) != 0)
            Sleep(0);
#else
        while (g_processCallbackDepth != 0)
        {
        }
#endif
    }

    static LONG ValidateDescriptor(const SharedRingBufferDescriptor& descriptor)
    {
        LONG status = kStatusNone;

        if (descriptor.descriptorMagic != kDescriptorMagic)
            status |= kStatusDescriptorMagicMismatch;

        if (descriptor.frames == NULL ||
            descriptor.sharedState == NULL ||
            descriptor.readIndex == NULL ||
            descriptor.writeIndex == NULL)
        {
            status |= kStatusNullPointer;
        }

        if (!IsAlignedPointer(descriptor.frames, sizeof(float)) ||
            !IsAlignedPointer(descriptor.sharedState, sizeof(SInt32)) ||
            !IsAlignedPointer(descriptor.readIndex, sizeof(SInt32)) ||
            !IsAlignedPointer(descriptor.writeIndex, sizeof(SInt32)))
        {
            status |= kStatusAlignmentInvalid;
        }

        if (descriptor.capacityFrames <= 1 ||
            !IsPowerOfTwo(descriptor.capacityFrames) ||
            descriptor.capacityMask != descriptor.capacityFrames - 1)
        {
            status |= kStatusCapacityInvalid;
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
        if (sharedCapacityFrames != descriptor.capacityFrames ||
            sharedCapacityMask != descriptor.capacityMask ||
            guardValueA != kSharedStateGuardValueA ||
            guardValueB != kSharedStateGuardValueB)
        {
            status |= kStatusSharedStateInvalid;
        }

        return status;
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
        EffectData* effectData = new EffectData;
        effectData->unused = 0;
        state->effectdata = effectData;
        return UNITY_AUDIODSP_OK;
    }

    UNITY_AUDIODSP_RESULT UNITY_AUDIODSP_CALLBACK ReleaseCallback(UnityAudioEffectState* state)
    {
        EffectData* effectData = state->GetEffectData<EffectData>();
        delete effectData;
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
        const int frameCount = (int)length;
        if (outbuffer == NULL || frameCount <= 0 || outchannels <= 0)
            return UNITY_AUDIODSP_OK;

        memset(outbuffer, 0, sizeof(float) * frameCount * outchannels);

        if (inbuffer != NULL && inchannels > 0)
        {
            const int passthroughChannels = (inchannels < outchannels) ? inchannels : outchannels;
            for (int frameIndex = 0; frameIndex < frameCount; ++frameIndex)
            {
                const int inputBase = frameIndex * inchannels;
                const int outputBase = frameIndex * outchannels;
                for (int channelIndex = 0; channelIndex < passthroughChannels; ++channelIndex)
                    outbuffer[outputBase + channelIndex] = inbuffer[inputBase + channelIndex];

                for (int channelIndex = passthroughChannels; channelIndex < outchannels; ++channelIndex)
                    outbuffer[outputBase + channelIndex] = inbuffer[inputBase];
            }
        }

        InterlockedIncrement(&g_processCallbackDepth);
        if (InterlockedCompareExchange(&g_hasSharedRingBuffer, 1, 1) == 0)
        {
            InterlockedDecrement(&g_processCallbackDepth);
            return UNITY_AUDIODSP_OK;
        }

        SharedRingBufferDescriptor ringBuffer = g_sharedRingBuffer;
        const LONG validationStatus = ValidateDescriptor(ringBuffer);
        if (validationStatus != kStatusNone)
        {
            InterlockedExchange(&g_hasSharedRingBuffer, 0);
            WriteStatusBits(validationStatus);
            InterlockedDecrement(&g_processCallbackDepth);
            return UNITY_AUDIODSP_OK;
        }

        const int rawReadIndex = ReadSharedIndex(ringBuffer.readIndex);
        const int rawWriteIndex = ReadSharedIndex(ringBuffer.writeIndex);
        if (rawReadIndex < 0 ||
            rawReadIndex >= ringBuffer.capacityFrames ||
            rawWriteIndex < 0 ||
            rawWriteIndex >= ringBuffer.capacityFrames)
        {
            InterlockedExchange(&g_hasSharedRingBuffer, 0);
            WriteStatusBits(kStatusSharedStateInvalid);
            InterlockedDecrement(&g_processCallbackDepth);
            return UNITY_AUDIODSP_OK;
        }

        int readIndex = rawReadIndex;
        const int writeIndex = rawWriteIndex;
        const int availableFrames = (writeIndex - readIndex) & ringBuffer.capacityMask;
        const int readableFrames = (availableFrames < frameCount) ? availableFrames : frameCount;

        for (int frameIndex = 0; frameIndex < readableFrames; ++frameIndex)
        {
            const float sample = ringBuffer.frames[(readIndex + frameIndex) & ringBuffer.capacityMask];
            const int outputBase = frameIndex * outchannels;
            for (int channelIndex = 0; channelIndex < outchannels; ++channelIndex)
                outbuffer[outputBase + channelIndex] += sample;
        }

        if (readableFrames > 0)
        {
            readIndex = (readIndex + readableFrames) & ringBuffer.capacityMask;
            WriteSharedIndex(ringBuffer.readIndex, readIndex);
        }

        WriteStatusBits(kStatusActive);
        InterlockedDecrement(&g_processCallbackDepth);
        return UNITY_AUDIODSP_OK;
    }

    extern "C" UNITY_AUDIODSP_EXPORT_API void AUDIO_CALLING_CONVENTION HectonSensoryKernel_RegisterSharedRingBuffer(const SharedRingBufferDescriptor* descriptor)
    {
        InterlockedExchange(&g_hasSharedRingBuffer, 0);
        WriteStatusBits(kStatusBusy);
        WaitForProcessCallbacksToDrain();

        if (descriptor == NULL)
        {
            ClearSharedRingBufferUnsafe();
            WriteStatusBits(kStatusCleared);
            return;
        }

        const LONG validationStatus = ValidateDescriptor(*descriptor);
        if (validationStatus != kStatusNone)
        {
            ClearSharedRingBufferUnsafe();
            WriteStatusBits(validationStatus);
            return;
        }

        g_sharedRingBuffer = *descriptor;
        WriteStatusBits(kStatusActive);
        InterlockedExchange(&g_hasSharedRingBuffer, 1);
    }

    extern "C" UNITY_AUDIODSP_EXPORT_API void AUDIO_CALLING_CONVENTION HectonSensoryKernel_ClearSharedRingBuffer()
    {
        InterlockedExchange(&g_hasSharedRingBuffer, 0);
        WriteStatusBits(kStatusBusy);
        WaitForProcessCallbacksToDrain();
        ClearSharedRingBufferUnsafe();
        WriteStatusBits(kStatusCleared);
    }

    extern "C" UNITY_AUDIODSP_EXPORT_API int AUDIO_CALLING_CONVENTION HectonSensoryKernel_GetSharedRingBufferStatus()
    {
        return (int)ReadStatusBits();
    }

    extern "C" UNITY_AUDIODSP_EXPORT_API int AUDIO_CALLING_CONVENTION HectonSensoryKernel_DebugProcessBlock(int frameCount, int outchannels)
    {
        if (frameCount <= 0 || frameCount > 4096 || outchannels <= 0 || outchannels > 8)
            return kStatusCapacityInvalid;

        const size_t sampleCount = (size_t)frameCount * (size_t)outchannels;
        float* scratch = (float*)malloc(sizeof(float) * sampleCount);
        if (scratch == NULL)
            return kStatusNullPointer;

        memset(scratch, 0, sizeof(float) * sampleCount);
        ProcessCallback(NULL, NULL, scratch, (unsigned int)frameCount, 0, outchannels);
        free(scratch);
        return (int)ReadStatusBits();
    }
}
