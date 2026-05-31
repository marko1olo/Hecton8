// Crest Ocean System

// Copyright 2020 Wave Harmonic Ltd

using UnityEngine;

namespace Crest
{
    public static class ComputeShaderHelpers
    {
        public const int MaxPortableThreadGroupSize = 256;
        public const int MaxDispatchGroupsPerDimension = 65535;

        public static ComputeShader LoadShader(string path)
        {
            // We provide this helper function to ensure the user gets a friendly error message in this error case
            ComputeShader computeShader = Resources.Load<ComputeShader>(path);
            Debug.Assert(computeShader != null,
                $"The shader {path} failed to load, this is likely due to an import error. Try right clicking the Crest folder in the Project view and selecting Reimport, and checking for errors.");
            return computeShader;
        }

        public static int DispatchCount(int elementCount, int threadGroupSize)
        {
            if (elementCount <= 0 || threadGroupSize <= 0)
            {
                return 0;
            }

            long groups = ((long)elementCount + threadGroupSize - 1L) / threadGroupSize;
            if (groups <= 0L || groups > MaxDispatchGroupsPerDimension)
            {
                return 0;
            }

            return (int)groups;
        }

        public static bool TryGetPortableKernelThreadGroupSizes(ComputeShader shader, int kernel, out int sizeX, out int sizeY, out int sizeZ)
        {
            sizeX = 0;
            sizeY = 0;
            sizeZ = 0;

            if (shader == null || kernel < 0 || !SystemInfo.supportsComputeShaders)
            {
                return false;
            }

            uint kernelSizeX;
            uint kernelSizeY;
            uint kernelSizeZ;
            try
            {
                if (!shader.IsSupported(kernel))
                {
                    return false;
                }

                shader.GetKernelThreadGroupSizes(kernel, out kernelSizeX, out kernelSizeY, out kernelSizeZ);
            }
            catch (System.ObjectDisposedException)
            {
                return false;
            }
            catch (System.InvalidOperationException)
            {
                return false;
            }
            catch (System.ArgumentException)
            {
                return false;
            }
            catch (MissingReferenceException)
            {
                return false;
            }
            catch (UnityException)
            {
                return false;
            }

            if (kernelSizeX == 0u || kernelSizeY == 0u || kernelSizeZ == 0u ||
                kernelSizeX > int.MaxValue || kernelSizeY > int.MaxValue || kernelSizeZ > int.MaxValue)
            {
                return false;
            }

            ulong totalThreadGroupSize = (ulong)kernelSizeX * kernelSizeY * kernelSizeZ;
            if (totalThreadGroupSize > MaxPortableThreadGroupSize)
            {
                return false;
            }

            sizeX = (int)kernelSizeX;
            sizeY = (int)kernelSizeY;
            sizeZ = (int)kernelSizeZ;
            return true;
        }

        public static bool TryGetPortableKernelThreadGroupSize1D(ComputeShader shader, int kernel, out int sizeX)
        {
            sizeX = 0;

            if (!TryGetPortableKernelThreadGroupSizes(shader, kernel, out int kernelSizeX, out int kernelSizeY, out int kernelSizeZ) ||
                kernelSizeY != 1 || kernelSizeZ != 1)
            {
                return false;
            }

            sizeX = kernelSizeX;
            return true;
        }

        public static bool TryGetPortableKernelThreadGroupSize2D(ComputeShader shader, int kernel, out int sizeX, out int sizeY)
        {
            sizeX = 0;
            sizeY = 0;

            if (!TryGetPortableKernelThreadGroupSizes(shader, kernel, out int kernelSizeX, out int kernelSizeY, out int kernelSizeZ) ||
                kernelSizeZ != 1)
            {
                return false;
            }

            sizeX = kernelSizeX;
            sizeY = kernelSizeY;
            return true;
        }

        public static bool TryFindKernel(ComputeShader shader, string kernelName, out int kernel)
        {
            kernel = -1;
            if (shader == null || string.IsNullOrEmpty(kernelName) || !SystemInfo.supportsComputeShaders)
            {
                return false;
            }

            try
            {
                if (!shader.HasKernel(kernelName))
                {
                    return false;
                }

                kernel = shader.FindKernel(kernelName);
                return kernel >= 0;
            }
            catch (System.ObjectDisposedException)
            {
                kernel = -1;
                return false;
            }
            catch (System.InvalidOperationException)
            {
                kernel = -1;
                return false;
            }
            catch (System.ArgumentException)
            {
                kernel = -1;
                return false;
            }
            catch (MissingReferenceException)
            {
                kernel = -1;
                return false;
            }
            catch (UnityException)
            {
                kernel = -1;
                return false;
            }
        }
    }
}
