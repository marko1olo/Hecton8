using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.UI
{
    public unsafe sealed partial class TerminalOsRuntime
    {
        private const BufferID TerminalInputStatesBufferId = (BufferID)71380;
        private const BufferID TerminalInputTelemetryRingBufferId = (BufferID)71381;
        private const BufferID TerminalInputTuningBufferId = (BufferID)71382;
        private const BufferID TerminalInputRowHashesBufferId = (BufferID)71383;
        private const string TerminalProjectionDumpRelativePath = "Docs/AgentLogs/Dump_1309_TerminalProjection.bin";
        private const uint TerminalProjectionFaultNonFinite = 1u << 16;
        private const uint TerminalProjectionFaultBudget = 1u << 17;
        private const uint TerminalProjectionFaultLayout = 1u << 18;
        private const uint TerminalProjectionRollbackExcluded = 1u;
        private const uint TerminalProjectionDirtyHashMask = 0x80000000u;
        private const uint TerminalProjectionHashValueMask = 0x7fffffffu;
        private static readonly int TerminalInputStatesId = Shader.PropertyToID("_TerminalInputStates");
        private static readonly int TerminalInputStateCountId = Shader.PropertyToID("_TerminalInputStateCount");

        [SerializeField, Range(0.0005f, 0.05f)] private float terminalCursorSnappingTolerance = 0.0065f;
        [SerializeField, Range(0.001f, 0.08f)] private float terminalRaycastThickness = 0.01f;

        private VaultGenerationHandle<TerminalInputStateDTO> _terminalInputStatesHandle;
        private VaultGenerationHandle<TerminalInputTelemetryEntry> _terminalInputTelemetryRingHandle;
        private VaultGenerationHandle<TerminalInputTuningDTO> _terminalInputTuningHandle;
        private VaultGenerationHandle<uint> _terminalInputRowHashesHandle;
        private GraphicsBuffer _terminalInputStateBuffer0;
        private GraphicsBuffer _terminalInputStateBuffer1;
        private GraphicsBuffer _terminalInputStateBuffer;
        private int _terminalInputWriteBufferIndex;
        private bool _terminalInputUploadDirty;
        private bool _terminalProjectionDumped;
        private string _terminalProjectionDumpFullPath;
        private int _terminalInputTelemetryCursor;
        private int _lastTerminalProjectionSignalsDispatched;
        private int _lastTerminalProjectionNonFiniteCount;
        private uint _terminalInputStateHash;
        private uint _terminalInputStateUploadedHash;
        private bool _terminalInputUploadInitialized;
        private float _lastTerminalProjectionEvalRadiusMeters;
        private float _terminalProjectionQualityCurvePower = 1f;
        private float _terminalProjectionLowRadiusMeters = 5f;
        private float _terminalProjectionUltraRadiusMeters = 25f;

        private void EnsureTerminalProjectionColdPaths(string projectRoot)
        {
            if (string.IsNullOrEmpty(_terminalProjectionDumpFullPath))
                _terminalProjectionDumpFullPath = Path.GetFullPath(Path.Combine(projectRoot, TerminalProjectionDumpRelativePath));
        }

        private void OpenTerminalProjectionNativeBuffers(IDataVault vault)
        {
            OpenNativeBufferForOwner(vault, TerminalInputStatesBufferId, _terminalCount, NativeArrayOptions.UninitializedMemory, out _terminalInputStatesHandle);
            OpenNativeBufferForOwner(vault, TerminalInputTelemetryRingBufferId, TerminalOsConstants.BlackBoxFrameCount, NativeArrayOptions.ClearMemory, out _terminalInputTelemetryRingHandle);
            OpenNativeBufferForOwner(vault, TerminalInputTuningBufferId, 1, NativeArrayOptions.UninitializedMemory, out _terminalInputTuningHandle);
            OpenNativeBufferForOwner(vault, TerminalInputRowHashesBufferId, _terminalCount, NativeArrayOptions.ClearMemory, out _terminalInputRowHashesHandle);
        }

        private bool ValidateTerminalProjectionNativeBuffers()
        {
            if (!TryOpenVaultBuffer(ref _terminalInputStatesHandle, out NativeArray<TerminalInputStateDTO> inputStates) ||
                !TryOpenVaultBuffer(ref _terminalInputTelemetryRingHandle, out NativeArray<TerminalInputTelemetryEntry> telemetryRing) ||
                !TryOpenVaultBuffer(ref _terminalInputTuningHandle, out NativeArray<TerminalInputTuningDTO> tuning) ||
                !TryOpenVaultBuffer(ref _terminalInputRowHashesHandle, out NativeArray<uint> rowHashes))
            {
                return false;
            }

            bool layoutValid =
                UnsafeUtility.SizeOf<TerminalInputStateDTO>() == TerminalOsConstants.TerminalInputStateStrideBytes &&
                UnsafeUtility.GetFieldOffset(typeof(TerminalInputStateDTO).GetField(nameof(TerminalInputStateDTO.TerminalAUP))) == 0 &&
                UnsafeUtility.GetFieldOffset(typeof(TerminalInputStateDTO).GetField(nameof(TerminalInputStateDTO.ForwardNormal))) == 24 &&
                UnsafeUtility.GetFieldOffset(typeof(TerminalInputStateDTO).GetField(nameof(TerminalInputStateDTO.UpVector))) == 36 &&
                UnsafeUtility.GetFieldOffset(typeof(TerminalInputStateDTO).GetField(nameof(TerminalInputStateDTO.ProjectedUV))) == 48 &&
                UnsafeUtility.GetFieldOffset(typeof(TerminalInputStateDTO).GetField(nameof(TerminalInputStateDTO.TerminalHashID))) == 56 &&
                UnsafeUtility.GetFieldOffset(typeof(TerminalInputStateDTO).GetField(nameof(TerminalInputStateDTO.InputFlags))) == 60 &&
                UnsafeUtility.SizeOf<TerminalInputGpuStateDTO>() == TerminalOsConstants.TerminalInputGpuStateStrideBytes &&
                UnsafeUtility.GetFieldOffset(typeof(TerminalInputGpuStateDTO).GetField(nameof(TerminalInputGpuStateDTO.ProjectedUV))) == 0 &&
                UnsafeUtility.GetFieldOffset(typeof(TerminalInputGpuStateDTO).GetField(nameof(TerminalInputGpuStateDTO.TerminalHashID))) == 8 &&
                UnsafeUtility.GetFieldOffset(typeof(TerminalInputGpuStateDTO).GetField(nameof(TerminalInputGpuStateDTO.InputFlags))) == 12 &&
                UnsafeUtility.GetFieldOffset(typeof(TerminalInputGpuStateDTO).GetField(nameof(TerminalInputGpuStateDTO.Reserved0))) == 16 &&
                UnsafeUtility.SizeOf<TerminalInputTelemetryEntry>() == TerminalOsConstants.TerminalInputTelemetryStrideBytes &&
                UnsafeUtility.SizeOf<TerminalInputTuningDTO>() == TerminalOsConstants.TerminalInputTuningStrideBytes &&
                UnsafeUtility.GetFieldOffset(typeof(TerminalInputTuningDTO).GetField(nameof(TerminalInputTuningDTO.MaxInteractionDistanceMeters))) == 0 &&
                UnsafeUtility.GetFieldOffset(typeof(TerminalInputTuningDTO).GetField(nameof(TerminalInputTuningDTO.CursorSnappingTolerance))) == 4 &&
                UnsafeUtility.GetFieldOffset(typeof(TerminalInputTuningDTO).GetField(nameof(TerminalInputTuningDTO.RaycastThickness))) == 8 &&
                UnsafeUtility.GetFieldOffset(typeof(TerminalInputTuningDTO).GetField(nameof(TerminalInputTuningDTO.QualityCurvePower))) == 12 &&
                UnsafeUtility.GetFieldOffset(typeof(TerminalInputTuningDTO).GetField(nameof(TerminalInputTuningDTO.LowRadiusMeters))) == 16 &&
                UnsafeUtility.GetFieldOffset(typeof(TerminalInputTuningDTO).GetField(nameof(TerminalInputTuningDTO.UltraRadiusMeters))) == 20;
            if (!layoutValid)
                _lastFaultFlags |= TerminalProjectionFaultLayout;

            return layoutValid &&
                   inputStates.Length >= _terminalCount &&
                   telemetryRing.Length >= TerminalOsConstants.BlackBoxFrameCount &&
                   tuning.Length >= 1 &&
                   rowHashes.Length >= _terminalCount;
        }

        private void ClearTerminalProjectionVaultHandles()
        {
            _terminalInputStatesHandle = default;
            _terminalInputTelemetryRingHandle = default;
            _terminalInputTuningHandle = default;
            _terminalInputRowHashesHandle = default;
        }

        private void ReleaseTerminalProjectionVaultHandles(IDataVault vault)
        {
            ReleaseVaultHandle(vault, ref _terminalInputStatesHandle, TerminalInputStatesBufferId);
            ReleaseVaultHandle(vault, ref _terminalInputTelemetryRingHandle, TerminalInputTelemetryRingBufferId);
            ReleaseVaultHandle(vault, ref _terminalInputTuningHandle, TerminalInputTuningBufferId);
            ReleaseVaultHandle(vault, ref _terminalInputRowHashesHandle, TerminalInputRowHashesBufferId);
        }

        private void InitializeTerminalProjectionState()
        {
            InitializeTerminalProjectionTuningState();

            if (!TryOpenVaultBuffer(ref _terminalInputStatesHandle, out NativeArray<TerminalInputStateDTO> inputStates) ||
                !TryOpenVaultBuffer(ref _terminalPlanesHandle, out NativeArray<TerminalPlaneDTO> planes) ||
                !TryOpenVaultBuffer(ref _terminalInputRowHashesHandle, out NativeArray<uint> rowHashes))
            {
                return;
            }

            int count = math.min(_terminalCount, math.min(inputStates.Length, planes.Length));
            for (int i = 0; i < count; i++)
            {
                TerminalPlaneDTO plane = planes[i];
                inputStates[i] = new TerminalInputStateDTO
                {
                    TerminalAUP = plane.CenterAup.ToAbsoluteDouble3(),
                    ForwardNormal = math.normalizesafe(plane.Normal, new float3(0f, 0f, -1f)),
                    UpVector = math.normalizesafe(plane.Up, new float3(0f, 1f, 0f)),
                    ProjectedUV = default,
                    TerminalHashID = plane.TerminalHash,
                    InputFlags = TerminalOsConstants.InteractionFlagInactive
                };
            }

            int hashCount = math.min(count, rowHashes.Length);
            for (int i = 0; i < hashCount; i++)
                rowHashes[i] = TerminalProjectionDirtyHashMask;

            _terminalInputUploadDirty = true;
            _terminalInputUploadInitialized = false;
            _terminalInputStateUploadedHash = 0u;
        }

        private void InitializeTerminalProjectionTuningState()
        {
            if (!TryOpenVaultBuffer(ref _terminalInputTuningHandle, out NativeArray<TerminalInputTuningDTO> tuning) ||
                tuning.Length == 0)
            {
                return;
            }

            TerminalInputTuningDTO value = new TerminalInputTuningDTO
            {
                MaxInteractionDistanceMeters = math.clamp(math.isfinite(interactionMaxDistanceMeters) ? interactionMaxDistanceMeters : 10f, 0.5f, 30f),
                CursorSnappingTolerance = math.clamp(math.isfinite(terminalCursorSnappingTolerance) ? terminalCursorSnappingTolerance : 0.0065f, 0.0005f, 0.05f),
                RaycastThickness = math.clamp(math.isfinite(terminalRaycastThickness) ? terminalRaycastThickness : 0.01f, 0.001f, 0.08f),
                QualityCurvePower = 1.0f,
                LowRadiusMeters = 5.0f,
                UltraRadiusMeters = 25.0f,
                TuningFlags = 1u
            };
            tuning[0] = value;
            interactionMaxDistanceMeters = value.MaxInteractionDistanceMeters;
            terminalCursorSnappingTolerance = value.CursorSnappingTolerance;
            terminalRaycastThickness = value.RaycastThickness;
            _terminalProjectionQualityCurvePower = value.QualityCurvePower;
            _terminalProjectionLowRadiusMeters = value.LowRadiusMeters;
            _terminalProjectionUltraRadiusMeters = value.UltraRadiusMeters;
        }

        private void RefreshTerminalProjectionTuningFromVault()
        {
            if (!TryOpenVaultBuffer(ref _terminalInputTuningHandle, out NativeArray<TerminalInputTuningDTO> tuning) ||
                tuning.Length == 0)
            {
                return;
            }

            TerminalInputTuningDTO value = tuning[0];
            interactionMaxDistanceMeters = math.clamp(math.isfinite(value.MaxInteractionDistanceMeters) ? value.MaxInteractionDistanceMeters : interactionMaxDistanceMeters, 0.5f, 30f);
            terminalCursorSnappingTolerance = math.clamp(math.isfinite(value.CursorSnappingTolerance) ? value.CursorSnappingTolerance : terminalCursorSnappingTolerance, 0.0005f, 0.05f);
            terminalRaycastThickness = math.clamp(math.isfinite(value.RaycastThickness) ? value.RaycastThickness : terminalRaycastThickness, 0.001f, 0.08f);
            _terminalProjectionQualityCurvePower = math.clamp(math.isfinite(value.QualityCurvePower) ? value.QualityCurvePower : _terminalProjectionQualityCurvePower, 0.25f, 4f);
            _terminalProjectionLowRadiusMeters = math.clamp(math.isfinite(value.LowRadiusMeters) ? value.LowRadiusMeters : _terminalProjectionLowRadiusMeters, 0.5f, 25f);
            _terminalProjectionUltraRadiusMeters = math.clamp(math.isfinite(value.UltraRadiusMeters) ? value.UltraRadiusMeters : _terminalProjectionUltraRadiusMeters, _terminalProjectionLowRadiusMeters, 30f);
        }

        private void EnsureTerminalProjectionGraphicsResources()
        {
            bool recreated = false;
            if (_terminalInputStateBuffer0 != null && _terminalInputStateBuffer0.count != _terminalCount)
            {
                ReleaseBuffer(ref _terminalInputStateBuffer0);
                _terminalInputStateBuffer = null;
                recreated = true;
            }

            if (_terminalInputStateBuffer1 != null && _terminalInputStateBuffer1.count != _terminalCount)
            {
                ReleaseBuffer(ref _terminalInputStateBuffer1);
                _terminalInputStateBuffer = null;
                recreated = true;
            }

            if (_terminalInputStateBuffer0 == null)
            {
                _terminalInputStateBuffer0 = CreateStructuredLockBuffer<TerminalInputGpuStateDTO>(_terminalCount);
                recreated = true;
            }
            if (_terminalInputStateBuffer1 == null)
            {
                _terminalInputStateBuffer1 = CreateStructuredLockBuffer<TerminalInputGpuStateDTO>(_terminalCount);
                recreated = true;
            }
            if (_terminalInputStateBuffer == null)
                _terminalInputStateBuffer = _terminalInputStateBuffer0;
            if (recreated)
            {
                _terminalInputStateUploadedHash = 0u;
                _terminalInputUploadInitialized = false;
                _terminalInputUploadDirty = true;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TerminalProjectionGraphicsReady()
        {
            return _terminalInputStateBuffer0 != null && _terminalInputStateBuffer1 != null && _terminalInputStateBuffer != null;
        }

        private void BindTerminalProjectionBuffers()
        {
            if (_terminalInputStateBuffer == null || terminalArrayMaterial == null)
                return;

            terminalArrayMaterial.SetBuffer(TerminalInputStatesId, _terminalInputStateBuffer);
            terminalArrayMaterial.SetFloat(TerminalInputStateCountId, _terminalCount);
        }

        private void DisposeTerminalProjectionGraphicsResources()
        {
            if (terminalArrayMaterial != null)
                terminalArrayMaterial.SetFloat(TerminalInputStateCountId, 0f);

            ReleaseBuffer(ref _terminalInputStateBuffer0);
            ReleaseBuffer(ref _terminalInputStateBuffer1);
            _terminalInputStateBuffer = null;
            _terminalInputWriteBufferIndex = 0;
            _terminalInputUploadDirty = false;
            _terminalInputUploadInitialized = false;
            _terminalInputStateHash = 0u;
            _terminalInputStateUploadedHash = 0u;
        }

        private void OnTerminalProjectionFinalized()
        {
            AuditTerminalProjectionSignals();
            if (_terminalInputUploadDirty)
                UploadTerminalInputStates();
        }

        private bool UploadTerminalInputStates()
        {
            if (!_terminalInputUploadDirty)
                return false;

            GraphicsBuffer uploadBuffer = _terminalInputWriteBufferIndex == 0 ? _terminalInputStateBuffer0 : _terminalInputStateBuffer1;
            if (uploadBuffer == null ||
                !TryOpenVaultBuffer(ref _terminalInputStatesHandle, out NativeArray<TerminalInputStateDTO> inputStates) ||
                !TryOpenVaultBuffer(ref _terminalInputRowHashesHandle, out NativeArray<uint> rowHashes))
            {
                return false;
            }

            int uploadCount = math.min(_terminalCount, math.min(inputStates.Length, math.min(rowHashes.Length, uploadBuffer.count)));
            if (uploadCount <= 0)
                return false;

            bool copied = false;
            bool failed = false;
            bool forceFullUpload = !_terminalInputUploadInitialized;
            int runStart = -1;
            for (int i = 0; i < uploadCount; i++)
            {
                bool rowDirty = forceFullUpload || (rowHashes[i] & TerminalProjectionDirtyHashMask) != 0u;
                if (rowDirty && runStart < 0)
                    runStart = i;
                if ((!rowDirty || i == uploadCount - 1) && runStart >= 0)
                {
                    int runEndExclusive = rowDirty && i == uploadCount - 1 ? i + 1 : i;
                    int runCount = runEndExclusive - runStart;
                    bool runCopied = UploadTerminalInputStateRun(uploadBuffer, inputStates, runStart, runCount);
                    if (runCopied)
                    {
                        copied = true;
                        for (int clearIndex = runStart; clearIndex < runEndExclusive; clearIndex++)
                            rowHashes[clearIndex] &= TerminalProjectionHashValueMask;
                    }
                    else
                    {
                        failed = true;
                    }

                    runStart = -1;
                }
            }

            if (copied && !failed)
            {
                _terminalInputStateBuffer = uploadBuffer;
                _terminalInputWriteBufferIndex ^= 1;
                _terminalInputUploadDirty = false;
                _terminalInputUploadInitialized = true;
                _terminalInputStateUploadedHash = _terminalInputStateHash;
                _bindingsDirty = true;
                BindTerminalProjectionBuffers();
            }

            return copied;
        }

        private static bool UploadTerminalInputStateRun(
            GraphicsBuffer uploadBuffer,
            NativeArray<TerminalInputStateDTO> inputStates,
            int startIndex,
            int count)
        {
            if (count <= 0)
                return true;

            bool copied = false;
            NativeArray<TerminalInputGpuStateDTO> mapped = uploadBuffer.LockBufferForWrite<TerminalInputGpuStateDTO>(startIndex, count);
            try
            {
                TerminalInputGpuStateDTO* destinationPtr = (TerminalInputGpuStateDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
                for (int i = 0; i < count; i++)
                {
                    TerminalInputStateDTO source = inputStates[startIndex + i];
                    destinationPtr[i] = new TerminalInputGpuStateDTO
                    {
                        ProjectedUV = source.ProjectedUV,
                        TerminalHashID = source.TerminalHashID,
                        InputFlags = source.InputFlags,
                        Reserved0 = default
                    };
                }

                copied = true;
            }
            finally
            {
                uploadBuffer.UnlockBufferAfterWrite<TerminalInputGpuStateDTO>(count);
            }

            return copied;
        }

        private void AuditTerminalProjectionSignals()
        {
            _lastEvaluatedTerminalCount = 0;
            _lastTerminalProjectionSignalsDispatched = 0;
            _lastTerminalProjectionNonFiniteCount = 0;
            _lastHoveredTerminalHash = 0u;
            uint stateHash = 2166136261u;

            if (!TryOpenVaultBuffer(ref _terminalInputStatesHandle, out NativeArray<TerminalInputStateDTO> inputStates) ||
                !TryOpenVaultBuffer(ref _terminalPlanesHandle, out NativeArray<TerminalPlaneDTO> planes) ||
                !TryOpenVaultBuffer(ref _buttonAabbHandle, out NativeArray<ButtonAABBDTO> buttons) ||
                !TryOpenVaultBuffer(ref _terminalInputRowHashesHandle, out NativeArray<uint> rowHashes))
            {
                return;
            }

            int count = math.min(_terminalCount, math.min(inputStates.Length, math.min(planes.Length, rowHashes.Length)));
            int buttonCount = math.min(_buttonCount, buttons.Length);
            for (int i = 0; i < count; i++)
            {
                TerminalInputStateDTO state = inputStates[i];
                uint flags = state.InputFlags;
                uint rowHash = ComputeTerminalInputStateHash(in state);
                stateHash = (stateHash ^ rowHash) * 16777619u;
                uint previousHash = rowHashes[i] & TerminalProjectionHashValueMask;
                if (rowHash != previousHash)
                    rowHashes[i] = rowHash | TerminalProjectionDirtyHashMask;
                else
                    rowHashes[i] = rowHash;
                if ((flags & TerminalOsConstants.InteractionFlagNonFinite) != 0u ||
                    !math.all(math.isfinite(state.ProjectedUV)) ||
                    !math.all(math.isfinite(state.ForwardNormal)) ||
                    !math.all(math.isfinite(state.UpVector)) ||
                    !math.all(math.isfinite(state.TerminalAUP)))
                {
                    _lastTerminalProjectionNonFiniteCount++;
                }

                if ((flags & TerminalOsConstants.InteractionFlagHover) == 0u)
                    continue;

                _lastEvaluatedTerminalCount++;
                _lastHoveredTerminalHash = state.TerminalHashID;
                if ((flags & TerminalOsConstants.InteractionFlagPress) == 0u)
                    continue;

                TerminalPlaneDTO plane = planes[i];
                int firstButton = (int)math.min(plane.LayoutFirstButton, (uint)buttonCount);
                int localButtonCount = (int)math.min(plane.LayoutButtonCount, (uint)math.max(0, buttonCount - firstButton));
                int buttonEnd = firstButton + localButtonCount;
                for (int buttonIndex = firstButton; buttonIndex < buttonEnd; buttonIndex++)
                {
                    ButtonAABBDTO button = buttons[buttonIndex];
                    if (button.TerminalHash != state.TerminalHashID ||
                        (button.Flags & TerminalOsConstants.ButtonFlagEnabled) == 0u)
                    {
                        continue;
                    }

                    float4 rect = button.RectUv;
                    float2 uv = state.ProjectedUV;
                    if (uv.x >= rect.x && uv.y >= rect.y && uv.x <= rect.z && uv.y <= rect.w)
                    {
                        _lastTerminalProjectionSignalsDispatched++;
                        break;
                    }
                }
            }

            _terminalInputStateHash = stateHash;
            _terminalInputUploadDirty = !_terminalInputUploadInitialized || stateHash != _terminalInputStateUploadedHash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ComputeTerminalInputStateHash(in TerminalInputStateDTO state)
        {
            uint hash = 2166136261u;
            hash = (hash ^ state.TerminalHashID) * 16777619u;
            hash = (hash ^ state.InputFlags) * 16777619u;
            hash = (hash ^ math.asuint(state.ProjectedUV.x)) * 16777619u;
            hash = (hash ^ math.asuint(state.ProjectedUV.y)) * 16777619u;
            return hash & TerminalProjectionHashValueMask;
        }

        private void RecordTerminalInputTelemetry(int frame, uint ownerFaultFlags)
        {
            if (!TryOpenVaultBuffer(ref _terminalInputTelemetryRingHandle, out NativeArray<TerminalInputTelemetryEntry> telemetryRing) ||
                telemetryRing.Length == 0)
            {
                return;
            }

            uint projectionFaults = ownerFaultFlags;
            if (_lastTerminalProjectionNonFiniteCount > 0)
                projectionFaults |= TerminalProjectionFaultNonFinite;
            if (_lastIntersectionMicroseconds > 200f)
                projectionFaults |= TerminalProjectionFaultBudget;

            int telemetryIndex = math.clamp(_terminalInputTelemetryCursor, 0, telemetryRing.Length - 1);
            telemetryRing[telemetryIndex] = new TerminalInputTelemetryEntry
            {
                Frame = frame,
                EvaluatedTerminals = math.min(_terminalCount, TerminalOsConstants.TerminalCapacity),
                SuccessfulProjections = _lastEvaluatedTerminalCount,
                SignalsDispatched = _lastTerminalProjectionSignalsDispatched,
                BurstMicroseconds = _lastIntersectionMicroseconds,
                EvalRadiusMeters = _lastTerminalProjectionEvalRadiusMeters,
                GlobalQualityWeight = _globalQualityWeight,
                FaultFlags = projectionFaults,
                HotPathAllocBytes = uint.MaxValue,
                RollbackExcluded = TerminalProjectionRollbackExcluded,
                LastHoveredTerminalHash = _lastHoveredTerminalHash,
                CursorSnappingTolerance = terminalCursorSnappingTolerance,
                RaycastThickness = terminalRaycastThickness,
                NonFiniteCount = _lastTerminalProjectionNonFiniteCount
            };
            _terminalInputTelemetryCursor = (_terminalInputTelemetryCursor + 1) % telemetryRing.Length;

            if ((projectionFaults & (TerminalProjectionFaultNonFinite | TerminalProjectionFaultBudget | TerminalProjectionFaultLayout)) != 0u)
                TryDumpTerminalInputBlackBox(projectionFaults);
        }

        private void TryDumpTerminalInputBlackBox(uint faultFlags)
        {
            if (_terminalProjectionDumped ||
                string.IsNullOrEmpty(_terminalProjectionDumpFullPath) ||
                !TryReadTerminalInputTelemetryDumpShape(out int telemetryLength, out int telemetryRingLength, out int telemetryCursor))
            {
                return;
            }

            try
            {
                WriteTerminalInputBlackBoxDump(_terminalProjectionDumpFullPath, faultFlags, telemetryLength, telemetryRingLength, telemetryCursor);
                _terminalProjectionDumped = true;
            }
            catch (IOException exception)
            {
                Hecton8.Core.H8Debug.LogException(exception);
            }
            catch (UnauthorizedAccessException exception)
            {
                Hecton8.Core.H8Debug.LogException(exception);
            }
            catch (NotSupportedException exception)
            {
                Hecton8.Core.H8Debug.LogException(exception);
            }
            catch (ArgumentException exception)
            {
                Hecton8.Core.H8Debug.LogException(exception);
            }
            catch (ObjectDisposedException exception)
            {
                Hecton8.Core.H8Debug.LogException(exception);
            }
            catch (InvalidOperationException exception)
            {
                Hecton8.Core.H8Debug.LogException(exception);
            }
        }

        private bool TryReadTerminalInputTelemetryDumpShape(out int telemetryLength, out int telemetryRingLength, out int telemetryCursor)
        {
            telemetryLength = 0;
            telemetryRingLength = 0;
            telemetryCursor = 0;
            if (!TryReadVaultBuffer(in _terminalInputTelemetryRingHandle, out NativeArray<TerminalInputTelemetryEntry> telemetryRing) ||
                telemetryRing.Length == 0)
            {
                return false;
            }

            telemetryRingLength = telemetryRing.Length;
            telemetryLength = math.min(TerminalOsConstants.BlackBoxFrameCount, telemetryRing.Length);
            telemetryCursor = _terminalInputTelemetryCursor;
            if (telemetryCursor < 0)
                telemetryCursor = 0;
            if (telemetryCursor >= telemetryRing.Length)
                telemetryCursor %= telemetryRing.Length;

            return telemetryLength > 0;
        }

        private bool TryReadTerminalInputTelemetryDumpEntry(int index, out TerminalInputTelemetryEntry entry)
        {
            entry = default;
            if (!TryReadVaultBuffer(in _terminalInputTelemetryRingHandle, out NativeArray<TerminalInputTelemetryEntry> telemetryRing) ||
                (uint)index >= (uint)telemetryRing.Length)
            {
                return false;
            }

            entry = telemetryRing[index];
            return _vault != null && !_vault.IsCompactionFenceActive;
        }

        private void WriteTerminalInputBlackBoxDump(string path, uint faultFlags, int telemetryLength, int telemetryRingLength, int telemetryCursor)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            int rowBytes = UnsafeUtility.SizeOf<TerminalInputTelemetryEntry>();
            using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            TerminalInputBlackBoxHeader header = new TerminalInputBlackBoxHeader
            {
                Magic = 0x33334853u,
                Version = 1u,
                FaultFlags = faultFlags,
                Cursor = (uint)telemetryCursor,
                EntryCount = (uint)telemetryLength,
                EntryStrideBytes = (uint)rowBytes,
                InputStateStrideBytes = (uint)UnsafeUtility.SizeOf<TerminalInputStateDTO>(),
                RollbackExcluded = TerminalProjectionRollbackExcluded
            };
            stream.Write(MemoryMarshal.CreateReadOnlySpan(
                ref UnsafeUtility.AsRef<byte>(UnsafeUtility.AddressOf(ref header)),
                UnsafeUtility.SizeOf<TerminalInputBlackBoxHeader>()));

            int count = (int)header.EntryCount;
            int start = telemetryCursor;

            for (int i = 0; i < count; i++)
            {
                int index = start + i;
                if (index >= telemetryRingLength)
                    index -= telemetryRingLength;

                TryReadTerminalInputTelemetryDumpEntry(index, out TerminalInputTelemetryEntry entry);
                stream.Write(MemoryMarshal.CreateReadOnlySpan(
                    ref UnsafeUtility.AsRef<byte>(UnsafeUtility.AddressOf(ref entry)),
                    rowBytes));
            }
        }

        public float GetTerminalProjectionMaxInteractionDistance()
        {
            return interactionMaxDistanceMeters;
        }

        public float GetTerminalProjectionCursorSnappingTolerance()
        {
            return terminalCursorSnappingTolerance;
        }

        public float GetTerminalProjectionRaycastThickness()
        {
            return terminalRaycastThickness;
        }

        public void ApplyTerminalProjectionEditorTuning(float maxInteractionDistance, float cursorSnappingTolerance, float raycastThickness)
        {
            TerminalInputTuningDTO value = new TerminalInputTuningDTO
            {
                MaxInteractionDistanceMeters = math.clamp(math.isfinite(maxInteractionDistance) ? maxInteractionDistance : interactionMaxDistanceMeters, 0.5f, 30f),
                CursorSnappingTolerance = math.clamp(math.isfinite(cursorSnappingTolerance) ? cursorSnappingTolerance : terminalCursorSnappingTolerance, 0.0005f, 0.05f),
                RaycastThickness = math.clamp(math.isfinite(raycastThickness) ? raycastThickness : terminalRaycastThickness, 0.001f, 0.08f),
                QualityCurvePower = 1.0f,
                LowRadiusMeters = 5.0f,
                UltraRadiusMeters = 25.0f,
                TuningFlags = 1u
            };

            if (TryOpenVaultBuffer(ref _terminalInputTuningHandle, out NativeArray<TerminalInputTuningDTO> tuning) &&
                tuning.Length > 0)
            {
                void* tuningPtr = NativeArrayUnsafeUtility.GetUnsafePtr(tuning);
                ref TerminalInputTuningDTO row = ref UnsafeUtility.AsRef<TerminalInputTuningDTO>(tuningPtr);
                row = value;
            }

            interactionMaxDistanceMeters = value.MaxInteractionDistanceMeters;
            terminalCursorSnappingTolerance = value.CursorSnappingTolerance;
            terminalRaycastThickness = value.RaycastThickness;
            _terminalProjectionQualityCurvePower = value.QualityCurvePower;
            _terminalProjectionLowRadiusMeters = value.LowRadiusMeters;
            _terminalProjectionUltraRadiusMeters = value.UltraRadiusMeters;
        }

        public bool TryGetLatestTerminalInputTelemetry(out TerminalInputTelemetryEntry entry)
        {
            entry = default;
            if (!TryReadVaultBuffer(in _terminalInputTelemetryRingHandle, out NativeArray<TerminalInputTelemetryEntry> telemetryRing) ||
                telemetryRing.Length == 0)
            {
                return false;
            }

            int index = _terminalInputTelemetryCursor - 1;
            if (index < 0)
                index = telemetryRing.Length - 1;
            entry = telemetryRing[index];
            return true;
        }

        public bool TryGetTerminalInputStateSnapshot(int index, out TerminalInputStateDTO state)
        {
            state = default;
            if (!TryReadVaultBuffer(in _terminalInputStatesHandle, out NativeArray<TerminalInputStateDTO> inputStates) ||
                index < 0 ||
                index >= math.min(_terminalCount, inputStates.Length))
            {
                return false;
            }

            state = inputStates[index];
            return true;
        }

#if UNITY_EDITOR
        private static void DrawTerminalInputProjectionGizmo(
            in TerminalPlaneDTO plane,
            in TerminalInputStateDTO inputState,
            NativeArray<GazeRayDTO> gazeRays)
        {
            if (gazeRays.IsCreated && gazeRays.Length > 0)
            {
                GazeRayDTO gaze = gazeRays[0];
                float3 origin = ResolveRuntimeLocalPosition(gaze.OriginAup, default);
                float3 direction = math.normalizesafe(gaze.Direction, new float3(0f, 0f, 1f));
                Gizmos.color = new Color(0.08f, 0.95f, 0.32f, 0.75f);
                Gizmos.DrawLine(ToVector3(origin), ToVector3(origin + direction * 3f));
            }

            if ((inputState.InputFlags & TerminalOsConstants.InteractionFlagHover) == 0u ||
                inputState.TerminalHashID != plane.TerminalHash)
            {
                return;
            }

            float3 center = ResolveRuntimeLocalPosition(plane.CenterAup, default);
            float3 right = math.normalizesafe(math.cross(inputState.UpVector, inputState.ForwardNormal), math.normalizesafe(plane.Right, new float3(1f, 0f, 0f)));
            float3 up = math.normalizesafe(inputState.UpVector, new float3(0f, 1f, 0f));
            float2 uv = ResolveTerminalProjectionHitPoint(inputState.ProjectedUV);
            float3 hit = center +
                         right * ((uv.x - 0.5f) * math.max(0.001f, plane.Width)) +
                         up * ((uv.y - 0.5f) * math.max(0.001f, plane.Height));
            Gizmos.color = new Color(1f, 0.04f, 0.02f, 0.95f);
            Gizmos.DrawWireSphere(ToVector3(hit), 0.045f);
        }
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ResolveRuntimeLocalPosition(AbsoluteUniversePosition targetAup, float3 fallback)
        {
            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            return AupPrecisionMath.LocalDeltaFloat3(
                targetAup.ToAbsoluteDouble3(),
                originAup.ToAbsoluteDouble3(),
                fallback);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float2 ResolveTerminalProjectionHitPoint(float2 uv)
        {
            return math.saturate(uv);
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct TerminalInputBlackBoxHeader
        {
            [FieldOffset(0)] public uint Magic;
            [FieldOffset(4)] public uint Version;
            [FieldOffset(8)] public uint FaultFlags;
            [FieldOffset(12)] public uint Cursor;
            [FieldOffset(16)] public uint EntryCount;
            [FieldOffset(20)] public uint EntryStrideBytes;
            [FieldOffset(24)] public uint InputStateStrideBytes;
            [FieldOffset(28)] public uint RollbackExcluded;
            [FieldOffset(32)] private uint _pad0;
            [FieldOffset(36)] private uint _pad1;
            [FieldOffset(40)] private uint _pad2;
            [FieldOffset(44)] private uint _pad3;
            [FieldOffset(48)] private uint _pad4;
            [FieldOffset(52)] private uint _pad5;
            [FieldOffset(56)] private uint _pad6;
            [FieldOffset(60)] private uint _pad7;
        }
    }
}
