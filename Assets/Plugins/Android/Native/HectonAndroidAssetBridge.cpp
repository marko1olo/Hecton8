#include <android/asset_manager.h>
#include <android/asset_manager_jni.h>
#include <android/log.h>
#include <jni.h>

#include <cerrno>
#include <cstddef>
#include <cstdint>
#include <cstdio>
#include <fcntl.h>
#include <limits>
#include <sys/stat.h>
#include <unistd.h>

#if defined(__SSE__) || defined(__x86_64__) || defined(__i386__)
#include <xmmintrin.h>
#endif

namespace
{
    constexpr int32_t H8_ERROR_INVALID_ARGUMENT = -1;
    constexpr int32_t H8_ERROR_JNI_ENVIRONMENT = -2;
    constexpr int32_t H8_ERROR_ASSET_MANAGER = -3;
    constexpr int32_t H8_ERROR_ASSET_MISSING = -4;
    constexpr int32_t H8_ERROR_BAD_LENGTH = -5;
    constexpr int32_t H8_ERROR_COMPRESSED_ASSET = -6;
    constexpr int32_t H8_DUMP_PATH_CAPACITY = 1024;
    constexpr int32_t H8_DUMP_HEADER_BYTES = 20;
    constexpr int32_t H8_DUMP_MAX_ENTRY_COUNT = 1024;
    constexpr int32_t H8_DUMP_MAX_ENTRY_BYTES = 256;

    JavaVM* H8_AsJavaVm(void* javaVm)
    {
        return reinterpret_cast<JavaVM*>(javaVm);
    }

    bool H8_TryAcquireJniEnvironment(void* javaVm, JNIEnv** environment, bool* attached)
    {
        JavaVM* vm = H8_AsJavaVm(javaVm);
        if (environment == nullptr || attached == nullptr || vm == nullptr)
            return false;

        *environment = nullptr;
        *attached = false;

        const jint getEnvResult = vm->GetEnv(reinterpret_cast<void**>(environment), JNI_VERSION_1_6);
        if (getEnvResult == JNI_OK)
            return *environment != nullptr;

        if (getEnvResult != JNI_EDETACHED)
            return false;

        if (vm->AttachCurrentThread(environment, nullptr) != JNI_OK)
            return false;

        *attached = true;
        return *environment != nullptr;
    }

    void H8_ReleaseJniEnvironment(void* javaVm, bool attached)
    {
        JavaVM* vm = H8_AsJavaVm(javaVm);
        if (attached && vm != nullptr)
            vm->DetachCurrentThread();
    }

    struct H8FloatingPointControlScope
    {
#if defined(__aarch64__)
        uint64_t Fpcr;
        uint64_t Fpsr;

        H8FloatingPointControlScope()
            : Fpcr(0)
            , Fpsr(0)
        {
            __asm__ volatile("mrs %0, fpcr" : "=r"(Fpcr));
            __asm__ volatile("mrs %0, fpsr" : "=r"(Fpsr));
        }

        ~H8FloatingPointControlScope()
        {
            __asm__ volatile("msr fpcr, %0" : : "r"(Fpcr));
            __asm__ volatile("msr fpsr, %0" : : "r"(Fpsr));
        }
#elif defined(__SSE__) || defined(__x86_64__) || defined(__i386__)
        uint32_t Mxcsr;

        H8FloatingPointControlScope()
            : Mxcsr(_mm_getcsr())
        {
        }

        ~H8FloatingPointControlScope()
        {
            _mm_setcsr(Mxcsr);
        }
#else
        H8FloatingPointControlScope() {}
        ~H8FloatingPointControlScope() {}
#endif

        H8FloatingPointControlScope(const H8FloatingPointControlScope&) = delete;
        H8FloatingPointControlScope& operator=(const H8FloatingPointControlScope&) = delete;
    };

    struct H8JniEnvironmentScope
    {
        H8FloatingPointControlScope FloatingPointScope;
        void* JavaVm;
        JNIEnv* Environment;
        bool Attached;

        explicit H8JniEnvironmentScope(void* javaVm)
            : JavaVm(javaVm)
            , Environment(nullptr)
            , Attached(false)
        {
            H8_TryAcquireJniEnvironment(JavaVm, &Environment, &Attached);
        }

        H8JniEnvironmentScope(const H8JniEnvironmentScope&) = delete;
        H8JniEnvironmentScope& operator=(const H8JniEnvironmentScope&) = delete;

        ~H8JniEnvironmentScope()
        {
            H8_ReleaseJniEnvironment(JavaVm, Attached);
        }

        bool IsValid() const
        {
            return Environment != nullptr;
        }
    };

    AAssetManager* H8_ResolveAssetManager(JNIEnv* environment, void* javaAssetManager)
    {
        if (environment == nullptr || javaAssetManager == nullptr)
            return nullptr;

        return AAssetManager_fromJava(environment, reinterpret_cast<jobject>(javaAssetManager));
    }

    int32_t H8_GetAssetLength(AAsset* asset)
    {
        if (asset == nullptr)
            return H8_ERROR_INVALID_ARGUMENT;

        const off64_t length = AAsset_getLength64(asset);
        if (length < 0 || length > static_cast<off64_t>(std::numeric_limits<int32_t>::max()))
            return H8_ERROR_BAD_LENGTH;

        return static_cast<int32_t>(length);
    }

    bool H8_IsFileDescriptorBacked(AAsset* asset, int32_t expectedLength)
    {
        if (asset == nullptr || expectedLength < 0)
            return false;

        off64_t start = 0;
        off64_t length = 0;
        const int fd = AAsset_openFileDescriptor64(asset, &start, &length);
        if (fd < 0)
            return false;

        close(fd);
        return start >= 0 && length == static_cast<off64_t>(expectedLength);
    }

    bool H8_TryMeasureCString(const char* value, int32_t capacity, size_t* length)
    {
        if (value == nullptr || length == nullptr || capacity <= 0)
            return false;

        for (int32_t index = 0; index < capacity; index++)
        {
            if (value[index] == '\0')
            {
                *length = static_cast<size_t>(index);
                return index > 0;
            }
        }

        return false;
    }

    bool H8_TryBuildChildPath(const char* basePath, const char* childPath, char* destination, int32_t capacity)
    {
        if (basePath == nullptr ||
            childPath == nullptr ||
            destination == nullptr ||
            capacity <= 0)
        {
            return false;
        }

        size_t baseLength = 0;
        size_t childLength = 0;
        if (!H8_TryMeasureCString(basePath, capacity, &baseLength) ||
            !H8_TryMeasureCString(childPath, capacity, &childLength))
        {
            return false;
        }

        const bool hasSlash = baseLength > 0 && basePath[baseLength - 1] == '/';
        const size_t requiredBytes = baseLength + childLength + (hasSlash ? 1u : 2u);
        if (requiredBytes > static_cast<size_t>(capacity))
            return false;

        const int written = hasSlash
            ? std::snprintf(destination, static_cast<size_t>(capacity), "%s%s", basePath, childPath)
            : std::snprintf(destination, static_cast<size_t>(capacity), "%s/%s", basePath, childPath);
        return written > 0 && written < capacity;
    }

    bool H8_EnsureDirectory(const char* path)
    {
        if (path == nullptr || path[0] == '\0')
            return false;

        if (mkdir(path, 0700) == 0)
            return true;

        if (errno != EEXIST)
            return false;

        struct stat status {};
        return stat(path, &status) == 0 && S_ISDIR(status.st_mode);
    }

    bool H8_WriteAll(int fd, const void* source, int32_t byteCount)
    {
        if (fd < 0 || source == nullptr || byteCount < 0)
            return false;

        const auto* cursor = static_cast<const uint8_t*>(source);
        int32_t writtenBytes = 0;
        while (writtenBytes < byteCount)
        {
            const ssize_t written = write(fd, cursor + writtenBytes, static_cast<size_t>(byteCount - writtenBytes));
            if (written < 0)
            {
                if (errno == EINTR)
                    continue;

                return false;
            }

            if (written == 0)
                return false;

            writtenBytes += static_cast<int32_t>(written);
        }

        return true;
    }

    void H8_WriteUInt32Le(uint8_t* destination, int32_t* offset, uint32_t value)
    {
        destination[(*offset)++] = static_cast<uint8_t>(value & 0xFFu);
        destination[(*offset)++] = static_cast<uint8_t>((value >> 8) & 0xFFu);
        destination[(*offset)++] = static_cast<uint8_t>((value >> 16) & 0xFFu);
        destination[(*offset)++] = static_cast<uint8_t>((value >> 24) & 0xFFu);
    }

    void H8_WriteInt32Le(uint8_t* destination, int32_t* offset, int32_t value)
    {
        H8_WriteUInt32Le(destination, offset, static_cast<uint32_t>(value));
    }

    bool H8_WriteTelemetryDumpFile(
        const char* dumpPath,
        const void* telemetryEntries,
        int32_t entryCount,
        int32_t entrySize,
        uint32_t status,
        int32_t cursor)
    {
        if (dumpPath == nullptr || dumpPath[0] == '\0')
            return false;

        const int fd = open(dumpPath, O_CREAT | O_WRONLY | O_TRUNC | O_CLOEXEC, 0600);
        if (fd < 0)
            return false;

        uint8_t header[H8_DUMP_HEADER_BYTES];
        int32_t headerOffset = 0;
        H8_WriteUInt32Le(header, &headerOffset, 0x4858444Du);
        H8_WriteUInt32Le(header, &headerOffset, status);
        int32_t normalizedCursor = cursor;
        if (normalizedCursor < 0 || normalizedCursor >= entryCount)
            normalizedCursor = 0;

        H8_WriteInt32Le(header, &headerOffset, normalizedCursor);
        H8_WriteInt32Le(header, &headerOffset, entryCount);
        H8_WriteInt32Le(header, &headerOffset, entrySize);

        bool ok = H8_WriteAll(fd, header, headerOffset);
        const auto* entryBytes = static_cast<const uint8_t*>(telemetryEntries);
        const int32_t firstEntryCount = entryCount - normalizedCursor;
        if (ok && firstEntryCount > 0)
        {
            ok = H8_WriteAll(
                fd,
                entryBytes + normalizedCursor * entrySize,
                firstEntryCount * entrySize);
        }

        if (ok && normalizedCursor > 0)
            ok = H8_WriteAll(fd, entryBytes, normalizedCursor * entrySize);

        if (close(fd) != 0)
            ok = false;

        return ok;
    }
}

extern "C" JNIEXPORT int32_t JNICALL H8_GetAssetSize(void* javaVm, void* assetManager, const char* filename)
{
    if (javaVm == nullptr || assetManager == nullptr || filename == nullptr || filename[0] == '\0')
        return H8_ERROR_INVALID_ARGUMENT;

    H8JniEnvironmentScope jniScope(javaVm);
    if (!jniScope.IsValid())
        return H8_ERROR_JNI_ENVIRONMENT;

    AAssetManager* resolvedAssetManager = H8_ResolveAssetManager(jniScope.Environment, assetManager);
    if (resolvedAssetManager == nullptr)
        return H8_ERROR_ASSET_MANAGER;

    AAsset* asset = AAssetManager_open(resolvedAssetManager, filename, AASSET_MODE_STREAMING);
    if (asset == nullptr)
        return H8_ERROR_ASSET_MISSING;

    int32_t length = H8_GetAssetLength(asset);
    if (length >= 0 && !H8_IsFileDescriptorBacked(asset, length))
        length = H8_ERROR_COMPRESSED_ASSET;

    AAsset_close(asset);
    return length;
}

extern "C" JNIEXPORT bool JNICALL H8_LoadAssetToPointer(
    void* javaVm,
    void* assetManager,
    const char* filename,
    void* destinationBuffer,
    int32_t bufferSize)
{
    if (javaVm == nullptr ||
        assetManager == nullptr ||
        filename == nullptr ||
        filename[0] == '\0' ||
        destinationBuffer == nullptr ||
        bufferSize <= 0)
    {
        return false;
    }

    H8JniEnvironmentScope jniScope(javaVm);
    if (!jniScope.IsValid())
        return false;

    AAssetManager* resolvedAssetManager = H8_ResolveAssetManager(jniScope.Environment, assetManager);
    if (resolvedAssetManager == nullptr)
        return false;

    AAsset* asset = AAssetManager_open(resolvedAssetManager, filename, AASSET_MODE_STREAMING);
    if (asset == nullptr)
        return false;

    const int32_t assetLength = H8_GetAssetLength(asset);
    if (assetLength < 0 ||
        assetLength != bufferSize ||
        !H8_IsFileDescriptorBacked(asset, assetLength))
    {
        AAsset_close(asset);
        return false;
    }

    auto* writeCursor = static_cast<uint8_t*>(destinationBuffer);
    int32_t totalRead = 0;
    while (totalRead < assetLength)
    {
        const int32_t remainingBytes = assetLength - totalRead;
        const int32_t chunkBytes = remainingBytes > 1048576 ? 1048576 : remainingBytes;
        const int read = AAsset_read(asset, writeCursor + totalRead, chunkBytes);
        if (read <= 0)
        {
            AAsset_close(asset);
            return false;
        }

        totalRead += read;
    }

    AAsset_close(asset);
    return totalRead == assetLength;
}

extern "C" JNIEXPORT bool JNICALL H8_WriteTelemetryDump(
    const char* persistentDataPath,
    const void* telemetryEntries,
    int32_t entryCount,
    int32_t entrySize,
    uint32_t status,
    int32_t cursor)
{
    if (persistentDataPath == nullptr ||
        persistentDataPath[0] == '\0' ||
        telemetryEntries == nullptr ||
        entryCount <= 0 ||
        entryCount > H8_DUMP_MAX_ENTRY_COUNT ||
        entrySize <= 0 ||
        entrySize > H8_DUMP_MAX_ENTRY_BYTES)
    {
        return false;
    }

    char docsPath[H8_DUMP_PATH_CAPACITY];
    char agentLogsPath[H8_DUMP_PATH_CAPACITY];
    char legacyDumpPath[H8_DUMP_PATH_CAPACITY];
    char agentDumpPath[H8_DUMP_PATH_CAPACITY];
    if (!H8_TryBuildChildPath(persistentDataPath, "Docs", docsPath, H8_DUMP_PATH_CAPACITY) ||
        !H8_TryBuildChildPath(persistentDataPath, "Docs/AgentLogs", agentLogsPath, H8_DUMP_PATH_CAPACITY) ||
        !H8_TryBuildChildPath(persistentDataPath, "Docs/AgentLogs/Dump_1404.bin", legacyDumpPath, H8_DUMP_PATH_CAPACITY) ||
        !H8_TryBuildChildPath(persistentDataPath, "Docs/AgentLogs/Dump_1504.bin", agentDumpPath, H8_DUMP_PATH_CAPACITY))
    {
        return false;
    }

    if (!H8_EnsureDirectory(docsPath) || !H8_EnsureDirectory(agentLogsPath))
        return false;

    const bool legacyOk = H8_WriteTelemetryDumpFile(
        legacyDumpPath,
        telemetryEntries,
        entryCount,
        entrySize,
        status,
        cursor);
    const bool agentOk = H8_WriteTelemetryDumpFile(
        agentDumpPath,
        telemetryEntries,
        entryCount,
        entrySize,
        status,
        cursor);
    return legacyOk && agentOk;
}
