#include "AudioPluginUtil.h"
#include <stdio.h>
#include <stddef.h>
#include <string.h>
#if !PLATFORM_WIN
#include <pthread.h>
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

    static volatile HectonAtomicInt32 g_telemetryDumpInUse = 0;
    static volatile HectonAtomicInt32 g_telemetryDumpBytes = 0;
    static unsigned char g_telemetryDumpBuffer[kTelemetryDumpMaxBytes] = {};

    static inline int WriteTelemetryDumpFile(const void* bytes, int byteCount)
    {
        if (bytes == NULL || byteCount <= 0 || byteCount > kTelemetryDumpMaxBytes)
            return 0;

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

#if PLATFORM_WIN
    static DWORD WINAPI TelemetryDumpThreadMain(LPVOID)
    {
        const int byteCount = (int)AtomicRead32(&g_telemetryDumpBytes);
        WriteTelemetryDumpFile(g_telemetryDumpBuffer, byteCount);
        AtomicWrite32(&g_telemetryDumpBytes, 0);
        AtomicWrite32(&g_telemetryDumpInUse, 0);
        return 0;
    }
#else
    static void* TelemetryDumpThreadMain(void*)
    {
        const int byteCount = (int)AtomicRead32(&g_telemetryDumpBytes);
        WriteTelemetryDumpFile(g_telemetryDumpBuffer, byteCount);
        AtomicWrite32(&g_telemetryDumpBytes, 0);
        AtomicWrite32(&g_telemetryDumpInUse, 0);
        return NULL;
    }
#endif

    static inline int QueueTelemetryDumpAsync(const void* bytes, int byteCount)
    {
        if (bytes == NULL || byteCount <= 0 || byteCount > kTelemetryDumpMaxBytes)
            return 0;

        if (AtomicIncrement32(&g_telemetryDumpInUse) != 1)
        {
            AtomicDecrement32(&g_telemetryDumpInUse);
            return 0;
        }

        memcpy(g_telemetryDumpBuffer, bytes, (size_t)byteCount);
        AtomicWrite32(&g_telemetryDumpBytes, (SInt32)byteCount);

#if PLATFORM_WIN
        HANDLE threadHandle = CreateThread(NULL, 0, TelemetryDumpThreadMain, NULL, 0, NULL);
        if (threadHandle == NULL)
        {
            AtomicWrite32(&g_telemetryDumpBytes, 0);
            AtomicWrite32(&g_telemetryDumpInUse, 0);
            return 0;
        }

        CloseHandle(threadHandle);
#else
        pthread_t threadHandle;
        if (pthread_create(&threadHandle, NULL, TelemetryDumpThreadMain, NULL) != 0)
        {
            AtomicWrite32(&g_telemetryDumpBytes, 0);
            AtomicWrite32(&g_telemetryDumpInUse, 0);
            return 0;
        }

        pthread_detach(threadHandle);
#endif

        return 1;
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
#endif
        }

        return AtomicRead32(&g_processCallbackDepth) == 0;
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

        AtomicIncrement32(&g_processCallbackDepth);
        if (AtomicRead32(&g_hasSharedRingBuffer) == 0)
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
            const int outputBase = frameIndex * outchannels;
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

    extern "C" UNITY_AUDIODSP_EXPORT_API void AUDIO_CALLING_CONVENTION HectonSensoryKernel_RegisterSharedRingBuffer(const SharedRingBufferDescriptor* descriptor)
    {
        AtomicWrite32(&g_hasSharedRingBuffer, 0);
        WriteStatusBits(kStatusBusy);
        if (!WaitForProcessCallbacksToDrain())
            return;

        if (descriptor == NULL)
        {
            ClearSharedRingBufferUnsafe();
            WriteStatusBits(kStatusCleared);
            return;
        }

        const SInt32 validationStatus = ValidateDescriptor(*descriptor);
        if (validationStatus != kStatusNone)
        {
            ClearSharedRingBufferUnsafe();
            WriteStatusBits(validationStatus);
            return;
        }

        g_sharedRingBuffer = *descriptor;
        WriteStatusBits(kStatusActive);
        AtomicWrite32(&g_hasSharedRingBuffer, 1);
    }

    extern "C" UNITY_AUDIODSP_EXPORT_API void AUDIO_CALLING_CONVENTION HectonSensoryKernel_ClearSharedRingBuffer()
    {
        AtomicWrite32(&g_hasSharedRingBuffer, 0);
        WriteStatusBits(kStatusBusy);
        if (!WaitForProcessCallbacksToDrain())
            return;

        ClearSharedRingBufferUnsafe();
        WriteStatusBits(kStatusCleared);
    }

    extern "C" UNITY_AUDIODSP_EXPORT_API int AUDIO_CALLING_CONVENTION HectonSensoryKernel_GetSharedRingBufferStatus()
    {
        return (int)ReadStatusBits();
    }

    extern "C" UNITY_AUDIODSP_EXPORT_API int AUDIO_CALLING_CONVENTION HectonSensoryKernel_DumpAudioBridgeTelemetry(const void* bytes, int byteCount)
    {
        return QueueTelemetryDumpAsync(bytes, byteCount);
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
