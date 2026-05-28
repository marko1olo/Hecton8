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
    private bool loadScheduled;
    private float loadAtTime;

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
            ScheduleLoadNextScene();
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

        if (loadScheduled && Time.time >= loadAtTime)
        {
            loadScheduled = false;
            LoadNextScene();
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
                ScheduleLoadNextScene();
                timerOn = true;
                if (TimeObject != null)
                {
                    TimeObject.SetActive(true);
                }
            };
        }
    }

    void OnCollisionEnter(Collision collision) {
        if (collision.gameObject.CompareTag("CandiceShockwaveCollider")) {
            if (!isIntro)
            {
                ScheduleLoadNextScene();
                timerOn = true;
                if (TimeObject != null)
                {
                    TimeObject.SetActive(true);
                }
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

    private void ScheduleLoadNextScene()
    {
        if (loadScheduled)
        {
            return;
        }

        timer = timeToNextScene;
        loadAtTime = Time.time + Mathf.Max(0f, timeToNextScene);
        loadScheduled = true;
    }

}
