using UnityEngine;
using Hecton8.Building;

namespace Hecton8.Environment
{
    /// <summary>
    /// Связующее звено между 3D-моделью камня и его точками крепления флоры.
    /// Вешается на финальный префаб камня.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RockDataLink : MonoBehaviour
    {
        [Header("═══ DATA ASSIGNMENT ═══")]
        [Tooltip("Перетащи сюда .asset файл сокетов, созданный через Rock Data Baker.")]
        public RockAttachmentData attachmentData;
    }
}