using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CandiceAIforGames.Data
{
    public class CandiceSaveManager : MonoBehaviour
    {
        public bool enableAutoSave;
        public bool enableFastMode;
        public int autoSaveInterval;
        public KeyCode fastSaveKey;
        public KeyCode fastLoadKey;
        public string storagePath;
        public string folderName;
        public GameObject savePanel;
        public GameObject container;
        public GameObject saveObject;
        public string saveType;
        private const string SAVEFILEEXTENSION = ".cndc";//The extension of the save file that will be generated.
        Vector3 pos;

        //public static ObjectsBL oBL;
        [HideInInspector]
        public static bool bProviderSelected;

        // Start is called before the first frame update
        void Start()
        {
            CandiceSaveSystem.Instance.Initialise(storagePath);
            InitialiseWeaponDB();
            LoadSaveItems();
            GetWeapons();
        }
        // Update is called once per frame
        void Update()
        {

        }


        public void Save(object obj, Text filename)
        {
            if (filename.text.Length < 1)
            {
                Debug.LogWarning("ERROR: Please enter a save filename");
                return;
            }
            CandiceSaveSystem.Instance.SaveToFile(obj, folderName + "/" + filename.text + SAVEFILEEXTENSION);
            LoadSaveItems();
        }







        void InitialiseWeaponDB()
        {
            CandiceWeapon weapon = new CandiceWeapon(0, "Long Sword2", "Sword", 22.5);
            AddWeaponToDB(weapon);
            weapon = new CandiceWeapon(1, "Short Sword", "Sword", 15.5);
            AddWeaponToDB(weapon);
        }
        public List<CandiceWeapon> GetWeapons()
        {
            string query = "SELECT * FROM weapon";
            CandiceSaveSystem.Instance.SetQuery(query);
            List<CandiceWeapon> weapons = new List<CandiceWeapon>(4);
            List<object> obj = CandiceSaveSystem.Instance.SelectAll();
            foreach (object o in obj)
            {
                CandiceWeapon weapon = o as CandiceWeapon;
                if (weapon == null)
                {
                    continue;
                }

                weapons.Add(weapon);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log("Name: " + weapon.WeaponName);
#endif
            }
            return weapons;
        }
        private void AddWeaponToDB(CandiceWeapon weapon)
        {
            if (!CandiceSaveSystem.Instance.DatabaseExists("TestDB"))
            {
                CandiceSaveSystem.Instance.CreateDatabase("TestDB");
            }
            Dictionary<object, object> parameters = new Dictionary<object, object>(4);
            parameters.Add("@WPN_ID", weapon.WeaponID);
            parameters.Add("@WPN_NAME", weapon.WeaponName);
            parameters.Add("@WPN_TYPE", weapon.WeaponType);
            parameters.Add("@WPN_DAMAGE", weapon.WeaponDamage);
            string query = "INSERT INTO weapon ([WPN_ID], [WPN_NAME], [WPN_TYPE], [WPN_DAMAGE])" +
                " VALUES (@WPN_ID, @WPN_NAME, @WPN_TYPE, @WPN_DAMAGE)";
            CandiceSaveSystem.Instance.SetQuery(query);
            CandiceSaveSystem.Instance.Insert(parameters);
        }

        public void LoadSaveItems()
        {
            Vector3 parentScale = new Vector3(container.transform.localScale.x, container.transform.localScale.y, container.transform.localScale.z);

            pos = new Vector3(parentScale.x / 2, 140, parentScale.z / 2);
            string[] filenames = CandiceSaveSystem.Instance.GetFileNames(folderName);

            bool hasSaveItemPrefab = saveObject.TryGetComponent<CandiceSaveItem>(out var saveItemPrefab);

            int childCount = container.transform.childCount;
            int i = 0;

            foreach (string file in filenames)
            {
                CandiceSaveItem saveItem = null;
                GameObject obj;

                if (i < childCount)
                {
                    obj = container.transform.GetChild(i).gameObject;
                    obj.SetActive(true);
                    obj.TryGetComponent(out saveItem);
                    obj.transform.localPosition = new Vector3(pos.x, pos.y, pos.z);
                }
                else
                {
                    if (hasSaveItemPrefab)
                    {
                        saveItem = Instantiate(saveItemPrefab, pos, Quaternion.identity);
                        obj = saveItem.gameObject;
                    }
                    else
                    {
                        obj = Instantiate(saveObject, pos, Quaternion.identity);
                        obj.TryGetComponent(out saveItem);
                    }
                    obj.transform.SetParent(container.transform, false);
                }


                int lastSlash = file.LastIndexOf('/');
                string fileName = lastSlash >= 0 ? file.Substring(lastSlash + 1) : file;

                if (saveItem != null)
                {
                    int lastDot = fileName.LastIndexOf('.');
                    saveItem.text.text = lastDot >= 0 ? fileName.Substring(0, lastDot) : fileName;
                    saveItem.path = folderName + "/" + fileName;

                }
                pos.y -= 35f;
                i++;
            }

            for (int j = i; j < childCount; j++)
            {
                container.transform.GetChild(j).gameObject.SetActive(false);
            }
        }
        private void ClearChildren(GameObject parent)
        {
            int count = parent.transform.childCount;
            for (int i = 0; i < count; i++)
            {
                Destroy(parent.transform.GetChild(i).gameObject);
            }
        }
        
    }
}

