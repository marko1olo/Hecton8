using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class TriggerNextScene : MonoBehaviour
{

    //load scenes async
    public static AsyncOperation loadingOperation;

    //time to next scene once trigger has been hit
    public float timeToNextScene = 5f;
    private bool timerOn = false;
    private float timer;

    //if checked, next scene will load based on timer, as soon as current scene loads
    public bool isIntro = false;
    
    //Loading UI Objects
    public GameObject Loading;
    public GameObject TimeObject;
    public Text TimeText;
    private string[] countdownLabels;
    private int lastDisplayedSeconds = int.MinValue;

    // Start is called before the first frame update
    void Start()
    {
        BuildCountdownLabels();

        if (TimeObject != null) {            
            timer = timeToNextScene;
            TimeObject.SetActive(false);
        }
        if (Loading != null) {
            Loading.SetActive(false);
        }
        if (isIntro) {
            StartCoroutine(myWaitCoroutine());
        };
        
    }

    // Update is called once per frame
    void Update()
    {
        if (TimeText != null && timerOn) {
            timer -= Time.deltaTime;
            int seconds = Mathf.Clamp(Mathf.RoundToInt(timer), 0, countdownLabels.Length - 1);
            if (seconds != lastDisplayedSeconds)
            {
                lastDisplayedSeconds = seconds;
                TimeText.text = countdownLabels[seconds];
            }
        }
    }

    private void BuildCountdownLabels()
    {
        int maxSeconds = Mathf.Max(0, Mathf.CeilToInt(timeToNextScene));
        // COLD ALLOC: string[maxSeconds + 1] - scene-transition countdown labels - owner: TriggerNextScene
        countdownLabels = new string[maxSeconds + 1];
        for (int i = 0; i < countdownLabels.Length; i++)
        {
            countdownLabels[i] = i.ToString();
        }
    }

    void OnTriggerEnter(Collider collider)
    {
        if (collider.gameObject.CompareTag("Player") || collider.gameObject.CompareTag("Projectile") || collider.gameObject.CompareTag("CandiceShockwaveCollider"))
        {
            if (!isIntro)
            {
                StartCoroutine(myWaitCoroutine());
                timerOn = true;
                TimeObject.SetActive(true);
            };
        }
    }

    void OnCollisionEnter(Collision collision) {
        if (collision.gameObject.CompareTag("CandiceShockwaveCollider")) {
            if (!isIntro)
            {
                StartCoroutine(myWaitCoroutine());
                timerOn = true;
                TimeObject.SetActive(true);
            };
        }
    }

    public void LoadNextScene()
    {
        if (Loading != null) {
            Loading.SetActive(true);
        }        
        loadingOperation = SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().buildIndex + 1,  LoadSceneMode.Single);
    }

    IEnumerator myWaitCoroutine()
    {
        yield return new WaitForSeconds(timeToNextScene);
        LoadNextScene();
    }

}
