using Unity.Cinemachine;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

[DefaultExecutionOrder(-100)]
public class GameManager : MonoBehaviour
{
    [System.Serializable]
    public struct StreetCameraData
    {
        public string streetName;
        public float cameraY;
    }

    private static GameManager _instance;
    public static GameManager Instance => _instance;

    private float _fixedDeltaTime;
    public bool IsGamePaused { get; private set; }
    public GameObject CurrentCar { get; private set; }
    public string CurrentStreet => _currentStreet;

    public event System.Action OnCarChanged;
    public event System.Action<bool> OnPauseStateChanged;
    private string _favoriteBrand;
    private const string FAVORITE_BRAND_KEY = "FavoriteBrand";

    [Header("UI References")]
    [SerializeField] private TMP_Text _streetNameDisplay;
    [SerializeField] private TMP_Text _houseNumberDisplay;
    [SerializeField] private TMP_Text _signifierDisplay;
    [SerializeField] private TMP_Text _discoveredStreetsText;
    [SerializeField] private float _signifierFadeDuration = 0.5f;
    [SerializeField] private TMP_Text _carNameText;

    [Header("Camera")]
    [SerializeField] private CinemachineCamera _virtualCamera;
    [SerializeField] private List<StreetCameraData> streetCameraSettings = new List<StreetCameraData>();

    [Header("Street System")]
    [SerializeField] private Transform _groundParent;

    private string _currentStreet;
    private HouseData _currentHouseData;
    private Coroutine _fadeCoroutine;
    private InputAction _pauseAction;

    private readonly HashSet<string> _discoveredStreets = new HashSet<string>();
    private int _discoveredStreetsCount = 0;
    private int _totalStreetsCount = 0;
    private const string DISCOVERED_STREETS_KEY = "DiscoveredStreets";
    private const string DISCOVERED_COUNT_KEY = "DiscoveredCount";

    [Header("Time System")]
    [SerializeField] private TimeSystem _timeSystem;
    public TimeSystem TimeSystem => _timeSystem;

    private PlayerFoodInventory _playerFoodInventory;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        _fixedDeltaTime = Time.fixedDeltaTime;

        _timeSystem = GetComponent<TimeSystem>() ?? gameObject.AddComponent<TimeSystem>();
        CountTotalStreets();
        LoadDiscoveredStreets();
        ValidateReferences();

        _pauseAction = new InputAction("Pause", binding: "<Keyboard>/escape");
        _pauseAction.performed += _ => TogglePause();
        _pauseAction.Enable();

        Invoke(nameof(LoadCarPosition), 0.5f);
    }

    private void Start()
    {
        _playerFoodInventory = FindFirstObjectByType<PlayerFoodInventory>();
        if (_playerFoodInventory == null)
        {
            Debug.LogWarning("PlayerFoodInventory not found in scene");
        }
    }


    private void OnDestroy()
    {
        _pauseAction?.Disable();
        _pauseAction?.Dispose();
    }

    public void TogglePause()
    {
        if (IsGamePaused) ResumeGame();
        else PauseGame();
    }

    private void ValidateReferences()
    {
        if (_streetNameDisplay == null)
            Debug.LogError("StreetNameDisplay reference is missing!", this);
        if (_houseNumberDisplay == null)
            Debug.LogError("HouseNumberDisplay reference is missing!", this);
        if (_virtualCamera == null)
            Debug.LogWarning("CinemachineVirtualCamera reference not set", this);
    }

    public void SetCurrentCar(GameObject car, bool suppressEvent = false)
    {
        if (car == CurrentCar) return;

        CurrentCar = car;

        if (!suppressEvent)
        {
            OnCarChanged?.Invoke();
        }

    }

    public void SetCurrentStreet(string streetName, Transform streetTransform = null)
    {
        if (string.IsNullOrEmpty(streetName)) return;

        _currentStreet = streetName;
        _currentHouseData = null;

        if (_streetNameDisplay != null)
        {
            _streetNameDisplay.text = streetName;
        }

        if (_discoveredStreets.Add(streetName))
        {
            _discoveredStreetsCount++;
            UpdateDiscoveredStreetsUI();
        }
    }

    public void SetCurrentHouse(string houseNumber, HouseData houseData)
    {
        if (string.IsNullOrEmpty(_currentStreet)) return;

        _currentHouseData = houseData;

        if (_houseNumberDisplay != null)
        {
            _houseNumberDisplay.text = houseNumber;
        }

        if (houseData != null)
        {
            if (houseData.betekenaar && !string.IsNullOrEmpty(houseData.betekenaarText))
            {
                ShowSignifierDisplay(houseData.betekenaarText);
            }
            else HideSignifierDisplay();
        }
        else HideSignifierDisplay();
    }

    public void PauseGame(bool showMenu = true)
    {
        Cursor.visible = true;
        IsGamePaused = true;
        Time.timeScale = 0f;
        Time.fixedDeltaTime = 0f;

        if (showMenu) OnPauseStateChanged?.Invoke(true);
    }

    public void ResumeGame()
    {
        IsGamePaused = false;
        Time.timeScale = 1f;
        Time.fixedDeltaTime = _fixedDeltaTime;
        OnPauseStateChanged?.Invoke(false);
    }

    private void ShowSignifierDisplay(string text)
    {
        if (_signifierDisplay == null) return;

        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);

        _signifierDisplay.text = text;
        _signifierDisplay.gameObject.SetActive(true);
        _fadeCoroutine = StartCoroutine(FadeText(0f, 1f, _signifierFadeDuration));
    }

    private void HideSignifierDisplay()
    {
        if (_signifierDisplay == null) return;

        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);

        _fadeCoroutine = StartCoroutine(FadeText(1f, 0f, _signifierFadeDuration, disableAfter: true));
    }

    private IEnumerator FadeText(float startAlpha, float endAlpha, float duration, bool disableAfter = false)
    {
        Color startColor = _signifierDisplay.color;
        Color endColor = new Color(startColor.r, startColor.g, startColor.b, endAlpha);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _signifierDisplay.color = Color.Lerp(startColor, endColor, elapsed / duration);
            yield return null;
        }

        _signifierDisplay.color = endColor;

        if (disableAfter)
        {
            _signifierDisplay.gameObject.SetActive(false);
            _signifierDisplay.color = new Color(startColor.r, startColor.g, startColor.b, 1f);
        }
    }

    #region Street Counter System
    private void CountTotalStreets()
    {
        GameObject streetsFolder = GameObject.FindWithTag("StreetFolder");
        if (streetsFolder == null)
        {
            Debug.LogWarning("StreetFolder tagged object not found!");
            return;
        }

        _groundParent = streetsFolder.transform.parent;

        if (_groundParent == null)
        {
            Debug.LogWarning("Could not find Streets parent folder");
            return;
        }

        _totalStreetsCount = 0;
        foreach (Transform child in _groundParent)
        {
            if (child == streetsFolder.transform) continue;
            _totalStreetsCount++;
        }
    }

    private void UpdateDiscoveredStreetsUI()
    {
        if (_discoveredStreetsText != null)
        {
            _discoveredStreetsText.text = $"Discovered Streets: {_discoveredStreetsCount}/{_totalStreetsCount}";
        }
    }

    private void LoadDiscoveredStreets()
    {
        _discoveredStreetsCount = PlayerPrefs.GetInt(DISCOVERED_COUNT_KEY, 0);
        for (int i = 0; i < _discoveredStreetsCount; i++)
        {
            string street = PlayerPrefs.GetString($"{DISCOVERED_STREETS_KEY}_{i}", "");
            if (!string.IsNullOrEmpty(street))
            {
                _discoveredStreets.Add(street);
            }
        }
        UpdateDiscoveredStreetsUI();
    }

    private void SaveDiscoveredStreets()
    {
        PlayerPrefs.SetInt(DISCOVERED_COUNT_KEY, _discoveredStreetsCount);
        int index = 0;
        foreach (string street in _discoveredStreets)
        {
            PlayerPrefs.SetString($"{DISCOVERED_STREETS_KEY}_{index}", street);
            index++;
        }
        PlayerPrefs.Save();
    }

    public void ResetDiscoveredStreets()
    {
        _discoveredStreets.Clear();
        _discoveredStreetsCount = 0;
        PlayerPrefs.DeleteKey(DISCOVERED_COUNT_KEY);

        int index = 0;
        while (PlayerPrefs.HasKey($"{DISCOVERED_STREETS_KEY}_{index}"))
        {
            PlayerPrefs.DeleteKey($"{DISCOVERED_STREETS_KEY}_{index}");
            index++;
        }
        UpdateDiscoveredStreetsUI();
    }
    #endregion

    #region Car Position Saving/Loading
    private void SaveCarPosition()
    {
        if (CurrentCar == null) return;

        PlayerPrefs.SetFloat(GameConstants.CAR_POSITION_X, CurrentCar.transform.position.x);
        PlayerPrefs.SetFloat(GameConstants.CAR_POSITION_Y, CurrentCar.transform.position.y);
        PlayerPrefs.SetFloat(GameConstants.CAR_ROTATION, CurrentCar.transform.rotation.eulerAngles.z);

        DriveCar driveCar = CurrentCar.GetComponentInChildren<DriveCar>();
        if (driveCar != null)
        {
            PlayerPrefs.SetInt(GameConstants.CAR_FACING, driveCar.GetCurrentFacingDirection());
        }
    }

    private void SaveCurrentStreet()
    {
        if (!string.IsNullOrEmpty(_currentStreet))
        {
            PlayerPrefs.SetString(GameConstants.CAR_STREET, _currentStreet);
        }
    }

    private void SaveCurrentCameraY()
    {
        LockYPosition cameraLock = FindFirstObjectByType<LockYPosition>();
        if (cameraLock != null)
        {
            PlayerPrefs.SetFloat(GameConstants.CAMERA_HEIGHT, cameraLock.transform.position.y);
        }
    }

    private void LoadCameraYForStreet(string streetName)
    {
        LockYPosition cameraLock = FindFirstObjectByType<LockYPosition>();
        if (cameraLock == null) return;

        if (PlayerPrefs.HasKey(GameConstants.CAMERA_HEIGHT))
        {
            cameraLock.SetYPosition(PlayerPrefs.GetFloat(GameConstants.CAMERA_HEIGHT));
            return;
        }

        foreach (var data in streetCameraSettings)
        {
            if (data.streetName == streetName)
            {
                cameraLock.SetYPosition(data.cameraY);
                break;
            }
        }
    }

    private void LoadCarPosition()
    {
        if (CurrentCar == null || !PlayerPrefs.HasKey(GameConstants.CAR_POSITION_X)) return;

        float posX = PlayerPrefs.GetFloat(GameConstants.CAR_POSITION_X);
        float posY = PlayerPrefs.GetFloat(GameConstants.CAR_POSITION_Y) + 0.5f; // Add 0.5 to Y
        Vector3 savedPosition = new Vector3(posX, posY, 0);

        float rotationZ = PlayerPrefs.GetFloat(GameConstants.CAR_ROTATION);
        Quaternion savedRotation = Quaternion.Euler(0, 0, rotationZ);

        CurrentCar.transform.SetPositionAndRotation(savedPosition, savedRotation);

        DriveCar driveCar = CurrentCar.GetComponentInChildren<DriveCar>();
        if (driveCar != null && PlayerPrefs.HasKey(GameConstants.CAR_FACING))
        {
            int savedFacing = PlayerPrefs.GetInt(GameConstants.CAR_FACING);
            int currentFacing = driveCar.GetCurrentFacingDirection();

            if (savedFacing != currentFacing)
            {
                driveCar.FlipCarWithWheels();
            }
        }

        if (PlayerPrefs.HasKey(GameConstants.CAR_STREET))
        {
            string savedStreet = PlayerPrefs.GetString(GameConstants.CAR_STREET);
            SetCurrentStreet(savedStreet);
            LoadCameraYForStreet(savedStreet);
        }

    }

    public void SetFavoriteBrand(string brand)
    {
        _favoriteBrand = brand;
        PlayerPrefs.SetString(FAVORITE_BRAND_KEY, brand);
        PlayerPrefs.Save();
        Debug.Log($"Saved Favorite Brand: {brand} (Key: {FAVORITE_BRAND_KEY})");
    }

    public void ResetSavedCarPosition()
    {
        PlayerPrefs.DeleteKey(GameConstants.CAR_POSITION_X);
        PlayerPrefs.DeleteKey(GameConstants.CAR_POSITION_Y);
        PlayerPrefs.DeleteKey(GameConstants.CAR_ROTATION);
        PlayerPrefs.DeleteKey(GameConstants.CAR_FACING);
        PlayerPrefs.DeleteKey(GameConstants.CAR_STREET);
        PlayerPrefs.DeleteKey(GameConstants.CAMERA_HEIGHT);
    }
    #endregion

    #region Save System

    private void SaveGameData()
    {
        // Save insurance claim first
        CrashScript crashScript = FindFirstObjectByType<CrashScript>();
        if (crashScript != null)
        {
            crashScript.SaveInsuranceClaim();
        }

        // Then save other data
        MainMenuController mainMenu = FindFirstObjectByType<MainMenuController>();
        if (mainMenu != null) mainMenu.TrackFreeRoamDistance();

        PoliceSystem policeSystem = FindFirstObjectByType<PoliceSystem>();
        if (policeSystem != null) policeSystem.SaveCurrentSpeedLimit();

        SaveCarPosition();
        SaveCurrentStreet();
        SaveCurrentCameraY();
        SaveDiscoveredStreets();

        if (CurrentCar != null)
        {
            // Use the existing method to get CarStats from the instance
            CarStats carStats = CurrentCar.GetComponentInChildren<CarStats>(true);
            if (carStats != null)
            {
                carStats.SaveInsuranceStatus();
                carStats.SaveOwnershipStatus(); // NEW: Save ownership status
            }
        }

        if (_playerFoodInventory != null) _playerFoodInventory.SaveFoodData();

        PlayerPrefs.Save();
    }


    public void SaveGame() => SaveGameData();
    #endregion

    private void OnApplicationQuit() => SaveGameData();
    private void OnApplicationPause(bool pauseStatus) { if (pauseStatus) SaveGameData(); }
    private void OnApplicationFocus(bool hasFocus) { if (!hasFocus) SaveGameData(); }

    public void ReturnToMainMenu()
    {
        SaveGameData();
        PauseGame();

        if (MainMenuController.Instance != null) MainMenuController.Instance.ReturnToMenu();
    }
}