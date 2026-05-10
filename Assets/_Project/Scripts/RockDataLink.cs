using UnityEngine;
using Hecton8.Building;

namespace Hecton8.Environment
{
    /// <summary>
    /// Svyazuyuschee zveno mezhdu 3D-modelyu kamnya i ego tochkami krepleniya flory.
    /// Veshaetsya na finalnyy prefab kamnya.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RockDataLink : MonoBehaviour
    {
        [Header("═══ DATA ASSIGNMENT ═══")]
        [Tooltip("Peretaschi syuda .asset fayl soketov, sozdannyy cherez Rock Data Baker.")]
        public RockAttachmentData attachmentData;
    }
}