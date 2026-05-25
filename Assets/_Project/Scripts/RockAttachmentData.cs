// ============================================================================
// HECTON-8 — RockAttachmentData.cs
// ScriptableObject: sohranennye pozitsii soketov dlya odnogo kamnya/gruppy.
//
// Hranit lokalnye koordinaty i tipy tochek krepleniya.
// Sozdaetsya avtomaticheski cherez RockDataBakerWindow.
// Ispolzuetsya v rantayme dlya protsedurnogo razmescheniya flory/narostov.
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
            [Tooltip("Pozitsiya soketa otnositelno kornya gruppy kamney.")]
            public float3 localPos;

            [Tooltip("Povorot soketa otnositelno kornya gruppy kamney.")]
            public quaternion localRot;

            [Tooltip("Tip soketa: Top, Side, Under.")]
            public HectonSocketHelper.SocketType type;

            /// <summary>
            /// Konvertiruet v mirovye koordinaty otnositelno ukazannogo Transform.
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
        [Tooltip("Vse tochki krepleniya na etom kamne.\n" +
                 "Avtomaticheski zapolnyaetsya cherez Rock Data Baker Window.")]
        public List<SocketData> sockets = new List<SocketData>(8);

        [Header("═══ METADATA ═══")]
        [Tooltip("Imya ishodnoy gruppy kamney pri eksporte.")]
        public string sourceGroupName;

        [Tooltip("Data poslednego eksporta.")]
        public string exportTimestamp;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>Kolichestvo soketov.</summary>
        public int Count => sockets.Count;

        /// <summary>Kolichestvo soketov ukazannogo tipa.</summary>
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
