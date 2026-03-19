// ============================================================================
// HECTON-8 — RockAttachmentData.cs
// ScriptableObject: сохранённые позиции сокетов для одного камня/группы.
//
// Хранит локальные координаты и типы точек крепления.
// Создаётся автоматически через RockDataBakerWindow.
// Используется в рантайме для процедурного размещения флоры/наростов.
// ============================================================================

using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Building
{
    [CreateAssetMenu(
        fileName = "NewRockAttachmentData",
        menuName = "Hecton/Building/Rock Attachment Data",
        order = 200)]
    public sealed class RockAttachmentData : ScriptableObject
    {
        // ══════════════════════════════════════════════════════════
        //  SOCKET DATA STRUCT
        // ══════════════════════════════════════════════════════════

        [Serializable]
        public struct SocketData
        {
            [Tooltip("Позиция сокета относительно корня группы камней.")]
            public float3 localPos;

            [Tooltip("Поворот сокета относительно корня группы камней.")]
            public quaternion localRot;

            [Tooltip("Тип сокета: Top, Side, Under.")]
            public HectonSocketHelper.SocketType type;

            /// <summary>
            /// Конвертирует в мировые координаты относительно указанного Transform.
            /// Zero GC — struct math only.
            /// </summary>
            public void ToWorld(Transform root, out Vector3 worldPos, out Quaternion worldRot)
            {
                worldPos = root.TransformPoint((Vector3)localPos);
                worldRot = root.rotation * (Quaternion)localRot;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  DATA
        // ══════════════════════════════════════════════════════════

        [Header("═══ SOCKET POINTS ═══")]
        [Tooltip("Все точки крепления на этом камне.\n" +
                 "Автоматически заполняется через Rock Data Baker Window.")]
        public List<SocketData> sockets = new List<SocketData>();

        [Header("═══ METADATA ═══")]
        [Tooltip("Имя исходной группы камней при экспорте.")]
        public string sourceGroupName;

        [Tooltip("Дата последнего экспорта.")]
        public string exportTimestamp;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>Количество сокетов.</summary>
        public int Count => sockets.Count;

        /// <summary>Количество сокетов указанного типа.</summary>
        public int CountByType(HectonSocketHelper.SocketType type)
        {
            int count = 0;
            for (int i = 0; i < sockets.Count; i++)
            {
                if (sockets[i].type == type)
                    count++;
            }
            return count;
        }
    }
}