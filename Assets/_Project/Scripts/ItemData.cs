namespace Hecton.Items
{
    using UnityEngine;

    /// <summary>
    /// Чистые данные предмета. Никакой логики — только описание.
    /// Создаётся через контекстное меню: Hecton → Item Data.
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewItem",
        menuName = "Hecton/Item Data",
        order    = 0)]
    public class ItemData : ScriptableObject
    {
        [Header("Identity")]
        public string   itemName    = "Unnamed Item";
        public Sprite   icon;
        [TextArea(2, 5)]
        public string   description = "";

        [Header("Properties")]
        public float    weight      = 1f;
        public bool     stackable   = true;
        public int      maxStack    = 64;

        [Header("Interaction")]
        [Tooltip("Глагол для подсказки: 'Забрать', 'Подобрать', 'Взять'")]
        public string   interactVerb = "Забрать";

        [Header("World")]
        [Tooltip("Префаб для выбрасывания в мир (опционально)")]
        public GameObject worldPrefab;

        // Готово к интеграции с инвентарём:
        // public ItemCategory category;
        // public ItemRarity   rarity;
        // public AudioClip    pickupSound;
    }
}