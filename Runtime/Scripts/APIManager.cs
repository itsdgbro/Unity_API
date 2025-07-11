using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class APIManager : MonoBehaviour
{
    //singleton
    public static APIManager Instance { get; private set; }

    //this event is invoked when api is successfully called, so listen to this event at loading screen (to complete the load bar), also update data on other script received from get api on this script after this event is invoked
    public event Action OnApiSuccess;

    #region Reference to JSPlugin
    [DllImport("__Internal")]
    private static extern string GetIframeData();
    #endregion

    #region API Properties

    // General Variables
    #region Head Data
    [SerializeField] private int gameID;
    private string nonceValue;
    private string domainName;
    private readonly string header = "X-WP-Nonce";
    private int userInstance;
    #endregion

    // getter and setter data
    #region Data Getter & Setter
    public bool soundOnOff { get; set; }
    public bool musicOnOff { get; set; }
    public string levelData { get; set; }
    public int level { get; set; }
    public int totalPoints { get; set; }
    public int pointsEarned { get; set; }
    public bool isLevelCompleted { get; set; }
    #endregion

    // API Response
    #region API Response
    [Header("API Response & Error")]
    private string APIResponse;
    private string APIError;
    public bool API_2_Success { get; set; }
    #endregion

    // API Endpoints
    #region API Gateways
    private readonly string gateway_1 = "/wp-admin/admin-ajax.php?action=get_rest_nonce";
    private readonly string gateway_2 = "/wp-json/chimpvine/v1/get-game-result";
    private readonly string gateway_3 = "/wp-json/chimpvine/v1/submit-game-result";
    private readonly string gateway_4 = "/wp-json/chimpvine/v1/update-game-result";
    #endregion
    #endregion


    [Header("Runtime Testing Variables")]

    //[Tooltip("Check this box for runtime testing only. Uncheck before building the game.")]
    [SerializeField] private bool disableAPI = false;
    private bool isForTesting;
    [Tooltip("Enter the domain name for testing purposes. e.g. http://localhost/wordpress")]
    private string domainNameTest;

    // UnityWebRequest object
    private UnityWebRequest unityWebRequest;

    public bool GetIsApiDisabled() => disableAPI;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    
    void Start()
    {
#if UNITY_EDITOR
            isForTesting = true;
#else
        isForTesting = false;
#endif

        if (disableAPI)
        {
            OnApiSuccess?.Invoke();
            Debug.LogWarning("APIManager: API is disabled.");
            return;
        }

        if (isForTesting)
        {
            domainName = string.IsNullOrEmpty(domainNameTest) ? "http://localhost/wordpress" : domainNameTest;
            StartCoroutine(API_1());
        }
        else
        {
            GetOrigin();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
    
    /// <summary>
    /// Retrieves the origin of the current application URL and updates the domain name.
    /// </summary>
    private void GetOrigin()
    {
        // retrive the server gateway
        if (string.IsNullOrEmpty(GetIframeData()))
        {
            Debug.LogError("Failed to retrieve the iframe 'data-game' attribute.");
            return;
        }
        else
        {
            // set the gateway path 
            domainName = GetIframeData();
            StartCoroutine(API_1());
        }
    }

    /// <summary>
    /// Sends a GET request to the API to retrieve a nonce value.
    /// </summary>
    /// <returns>An IEnumerator for coroutine handling.</returns>
    public IEnumerator API_1()
    {
        if (disableAPI) yield break;
        using (unityWebRequest = UnityWebRequest.Get(domainName + gateway_1))
        {
            yield return unityWebRequest.SendWebRequest();

            if (unityWebRequest.result == UnityWebRequest.Result.ConnectionError || unityWebRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError("Server Response: " + unityWebRequest.downloadHandler.text);
            }
            else
            {
                string jsonResponse = unityWebRequest.downloadHandler.text;

                // Deserialize the JSON response
                API_1_Response response = JsonUtility.FromJson<API_1_Response>(jsonResponse);

                // API 1 200/Ok
                if (response.success)
                {
                    nonceValue = response.data;
                    StartCoroutine(API_2());
                }
            }
        }
    }

    /// <summary>
    /// Sends a GET request to the API to retrieve game result data.
    /// </summary>
    /// <returns>An IEnumerator for coroutine handling.</returns>
    public IEnumerator API_2()
    {
        if (disableAPI) yield break;
        using (unityWebRequest = UnityWebRequest.Get((domainName + gateway_2) + "?gameid=" + gameID))
        {
            unityWebRequest.SetRequestHeader(header, nonceValue);

            yield return unityWebRequest.SendWebRequest();

            if (unityWebRequest.result == UnityWebRequest.Result.ConnectionError || unityWebRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError("Server Response: " + unityWebRequest.downloadHandler.text);
            }
            else
            {
                API_2_Response response = JsonUtility.FromJson<API_2_Response>(unityWebRequest.downloadHandler.text);
                level = response.Level;
                API_2_Success = true;
                OnApiSuccess?.Invoke();
            }
        }
    }

    /// <summary>
    /// Sends a POST request to the API to indicate the start of a game level.
    /// </summary>
    /// <param name="level">The current game level.</param>
    /// <param name="soundOnOff">Indicates whether the sound is on or off.</param>
    /// <param name="musicOnOff">Indicates whether the music is on or off.</param>
    /// <returns>An IEnumerator for coroutine handling.</returns>
    public IEnumerator Post_API_Start(
        int level,
        bool soundOnOff,
        bool musicOnOff)
    {
        if (disableAPI) yield break;
        String url = domainName + gateway_3;
        Post_Start_Dao postData = new()
        {
            GameID = gameID,
            Level = level,
            GameStartLocalDateTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            SoundOnOff = soundOnOff ? 1 : 0,
            MusicOnOff = musicOnOff ? 1 : 0,
            LevelPassed = 0,
            islevelend = 0
        };

        // Convert to JSON
        string jsonData = JsonUtility.ToJson(postData);
        byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonData);

        // Create a UnityWebRequest
        using (unityWebRequest = new(url, "POST")
        {
            uploadHandler = new UploadHandlerRaw(jsonBytes),
            downloadHandler = new DownloadHandlerBuffer()
        })
        {
            // Set headers
            unityWebRequest.SetRequestHeader("Content-Type", "application/json");
            unityWebRequest.SetRequestHeader(header, nonceValue);
            // Send request
            yield return unityWebRequest.SendWebRequest();

            // Handle response
            if (unityWebRequest.result == UnityWebRequest.Result.ConnectionError || unityWebRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError("Server Response: " + unityWebRequest.downloadHandler.text);
            }
            else
            {
                APIResponse = unityWebRequest.downloadHandler.text;
                API_3_Response response = JsonUtility.FromJson<API_3_Response>(unityWebRequest.downloadHandler.text);
                userInstance = response.userinstance;
            }
        }
    }

    /// Sends a POST request to the API endpoint to submit game end data.
    /// </summary>
    /// <param name="pointsEarned">The number of points earned by the user.</param>
    /// <param name="totalPoints">The total number of points available.</param>
    /// <param name="levelData">The data related to the level played.</param>
    /// <param name="isLevelCompleted">Indicates whether the level was completed successfully.</param>
    /// <returns>An IEnumerator for coroutine handling.</returns>
    public IEnumerator Post_API_End(
        int pointsEarned,
        int totalPoints,
        string levelData,
        bool isLevelCompleted
        )
    {
        if (disableAPI) yield break;
        String url = domainName + gateway_4;

        Post_End_Dao postData = new()
        {
            userinstance = userInstance,
            PointsEarned = pointsEarned,
            TotalPoints = totalPoints,
            GameEndLocalDateTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            LevelPassed = isLevelCompleted ? 1 : 0,
            islevelend = 1,
            LevelData = levelData
        };

        // Convert to JSON
        string jsonData = JsonUtility.ToJson(postData);
        byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonData);

        // Create a UnityWebRequest
        using (unityWebRequest = new(url, "POST")
        {
            uploadHandler = new UploadHandlerRaw(jsonBytes),
            downloadHandler = new DownloadHandlerBuffer()
        })
        {
            // Set headers
            unityWebRequest.SetRequestHeader("Content-Type", "application/json");
            unityWebRequest.SetRequestHeader(header, nonceValue);

            // Send request
            yield return unityWebRequest.SendWebRequest();

            // Handle response
            if (unityWebRequest.result == UnityWebRequest.Result.ConnectionError || unityWebRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError("Server Response: " + unityWebRequest.downloadHandler.text);
            }
            else
            {
                APIResponse = unityWebRequest.downloadHandler.text;
                API_3_Response response = JsonUtility.FromJson<API_3_Response>(unityWebRequest.downloadHandler.text);
                userInstance = response.userinstance;
            }
        }
    }

    #region EACH API RESPONSE DAO
    // store API 1 response
    public class API_1_Response
    {
        public bool success;
        public string data;
    }

    public class API_2_Response
    {
        public int Level;
    }

    public class API_3_Response
    {
        public string status;
        public int userinstance;
    }

    public class Post_Start_Dao
    {
        public int GameID;
        public int Level;
        public string GameStartLocalDateTime;
        public int SoundOnOff;
        public int MusicOnOff;
        public int LevelPassed;
        public int islevelend;
    }

    public class Post_End_Dao
    {
        public int userinstance;
        public int PointsEarned;
        public int TotalPoints;
        public string GameEndLocalDateTime;
        public int LevelPassed;
        public int islevelend;
        public string LevelData;
    }
    #endregion
}
