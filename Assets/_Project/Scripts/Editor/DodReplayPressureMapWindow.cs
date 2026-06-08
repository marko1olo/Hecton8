#if UNITY_EDITOR
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Editor-only 2D pressure/gas grid view fed by the replay atmosphere sidecar.
    /// </summary>
    public sealed class DodReplayPressureMapWindow : EditorWindow
    {
        private const int CellCapacity = 256;
        private const float NominalPressureKpa = HectonSurvivalContract.KPaPerAtmosphere;
        private const string NativeMemoryOwner = nameof(DodReplayPressureMapWindow);
        private const string CellsLabel = "cells";

        private NativeArray<DodReplayAtmosphereCellRecord> _cells;
        private int _cellsSentinelId;
        private int _cellCount;

        [MenuItem("Hecton8/Forensics/DOD Atmosphere Pressure Map")]
        private static void Open()
        {
            GetWindow<DodReplayPressureMapWindow>("DOD Pressure");
        }

        private void OnEnable()
        {
            _cells = new NativeArray<DodReplayAtmosphereCellRecord>(CellCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<DodReplayAtmosphereCellRecord>[256] - editor pressure map staging - owner: DodReplayPressureMapWindow
            try
            {
                _cellsSentinelId = NativeMemorySentinel.RegisterNativeArray(
                    _cells,
                    NativeMemoryOwner,
                    CellsLabel,
                    NativeAllocationLifetime.Session);
                if (_cellsSentinelId <= 0)
                    throw new System.InvalidOperationException($"Native memory sentinel registration failed for {CellsLabel}.");
            }
            catch
            {
                _cells.Dispose();
                _cells = default;
                _cellsSentinelId = 0;
                throw;
            }
            EditorApplication.update -= Repaint;
            EditorApplication.update += Repaint;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Repaint;
            System.Exception cleanupException = null;

            if (_cellsSentinelId > 0)
            {
                try
                {
                    NativeMemorySentinel.Unregister(_cellsSentinelId);
                }
                catch (System.Exception exception)
                {
                    cleanupException = exception;
                }
                finally
                {
                    _cellsSentinelId = 0;
                }
            }

            if (_cells.IsCreated)
            {
                try
                {
                    _cells.Dispose();
                }
                catch (System.Exception exception)
                {
                    if (cleanupException == null)
                        cleanupException = exception;
                }
                finally
                {
                    _cells = default;
                }
            }
            else
            {
                _cells = default;
            }

            if (cleanupException != null)
                throw cleanupException;
        }

        private void OnGUI()
        {
            if (!_cells.IsCreated)
                return;

            _cellCount = DodReplayRecorder.CopyAtmosphereCells(_cells);
            EditorGUILayout.LabelField("Cells", _cellCount.ToString());
            Rect gridRect = GUILayoutUtility.GetRect(320f, 320f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(gridRect, new Color(0.03f, 0.04f, 0.05f, 1f));

            if (_cellCount <= 0)
            {
                GUI.Label(gridRect, "No atmosphere replay cells.");
                return;
            }

            ResolveGridBounds(out int minX, out int maxX, out int minY, out int maxY);
            int width = math.max(1, maxX - minX + 1);
            int height = math.max(1, maxY - minY + 1);
            float cellWidth = gridRect.width / width;
            float cellHeight = gridRect.height / height;

            for (int i = 0; i < _cellCount; i++)
            {
                DodReplayAtmosphereCellRecord cell = _cells[i];
                int x = cell.X - minX;
                int y = cell.Y - minY;
                Rect cellRect = new Rect(
                    gridRect.x + x * cellWidth,
                    gridRect.yMax - (y + 1) * cellHeight,
                    math.max(1f, cellWidth - 1f),
                    math.max(1f, cellHeight - 1f));
                EditorGUI.DrawRect(cellRect, ResolveCellColor(cell));
            }
        }

        private void ResolveGridBounds(out int minX, out int maxX, out int minY, out int maxY)
        {
            minX = int.MaxValue;
            maxX = int.MinValue;
            minY = int.MaxValue;
            maxY = int.MinValue;
            for (int i = 0; i < _cellCount; i++)
            {
                DodReplayAtmosphereCellRecord cell = _cells[i];
                minX = math.min(minX, cell.X);
                maxX = math.max(maxX, cell.X);
                minY = math.min(minY, cell.Y);
                maxY = math.max(maxY, cell.Y);
            }
        }

        private static Color ResolveCellColor(DodReplayAtmosphereCellRecord cell)
        {
            float pressure = math.saturate(cell.PressureKpa / (NominalPressureKpa * 2f));
            float oxygen = math.saturate(cell.Oxygen01);
            float carbon = math.saturate(cell.CarbonDioxide01);
            return new Color(
                math.saturate(pressure + carbon * 0.45f),
                math.saturate(oxygen * 0.85f + pressure * 0.2f),
                math.saturate(1f - pressure * 0.35f),
                1f);
        }
    }
}
#endif
