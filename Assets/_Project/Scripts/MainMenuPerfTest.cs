using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;
using Hecton.UI.MainMenu;

public class MainMenuPerfTest : MonoBehaviour
{
    public void Start()
    {
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;

        // Setup dummy hierarchy
        GameObject root = new GameObject("Root");
        for (int i = 0; i < 10; i++)
        {
            GameObject child = new GameObject($"Child_{i}");
            child.transform.SetParent(root.transform);
            child.AddComponent<Image>();

            for (int j = 0; j < 10; j++)
            {
                GameObject grandchild = new GameObject($"Grandchild_{j}");
                grandchild.transform.SetParent(child.transform);
                grandchild.AddComponent<Image>();

                for (int k = 0; k < 10; k++)
                {
                    GameObject ggchild = new GameObject($"GGChild_{k}");
                    ggchild.transform.SetParent(grandchild.transform);
                    ggchild.AddComponent<Image>();
                }
            }
        }

        // Root has selectable
        root.AddComponent<Button>();

        // We can't call private method easily without reflection
    }
}
