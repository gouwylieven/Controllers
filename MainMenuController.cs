using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DefaultExecutionOrder(-90)]
public class MainMenuController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] public GameObject mainMenuCanvas;
    [SerializeField] public Button freeRoamButton;
    [SerializeField] public Button uberButton;
    [SerializeField] private Button noButton;
    [SerializeField] private Button yesButton;
    [SerializeField] private TMP_Text speedometerText;
    [SerializeField] private TMP_Text odometerText;
    [SerializeField] private GameObject RPMMeter;

    [SerializeField] private GameObject GameUI;

    [Header("Button Text Colors")]
    [SerializeField] private DirectTextColorChange uberTextColorChanger;

    [Header("Audio")]
    [SerializeField] private AudioClip menuOpenSound;
    [SerializeField] private AudioClip menuCloseSound;
    [SerializeField] private AudioClip buttonClickSound;

    [Header("Button Colors")]
    [SerializeField] private Color normalButtonColor = Color.white;
    [SerializeField] private Color clickedButtonColor = Color.gray;

    [Header("Time UI")]
    [SerializeField] private ClockUI clockUI;

    [Header("Job Receipt UI")]
    [SerializeField] public GameObject ticketFolder;
    [SerializeField] private Button exitButton;
    [SerializeField] private Button newJobButton;

    [Header("Police")]
    [SerializeField] private PoliceSpawner policeSpawner;

    [Header("Speed Limit Display")]
    [SerializeField] private SpeedLimitDisplayManager _speedLimitDisplayManager;



    private GameObject _activeCar;
    private AudioSource _audioSource;
    private static MainMenuController _instance;
    private ColorBlock _originalFreeRoamButtonColors;
    private ColorBlock _originalUberButtonColors;
    private bool _isTransitioning = false;
    private RideRequestGenerator _rideRequestGenerator;
    private PlayerInput _playerInput;
    private float freeRoamStartOdometer;
    private bool isFreeRoamActive = false;


    public static MainMenuController Instance { get { return _instance; } }

    private bool IntroMenuCompleted()
    {
        return PlayerPrefs.GetInt(GameConstants.INTRO_MENU_COMPLETED, 0) == 1;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPauseStateChanged -= HandlePauseState;
            GameManager.Instance.OnCarChanged -= HandleCarChanged; // ← ADD THIS
        }
    }

    private void HandleCarChanged()
    {
        FindActiveCar();

        // Also update any UI that depends on the current car
        if (_activeCar != null)
        {
            // Enable/disable input based on car presence
            _playerInput = _activeCar.GetComponent<PlayerInput>();
            if (_playerInput != null)
            {
                _playerInput.actions.FindAction("Eat").Disable();
            }

            // Update any other car-dependent UI here
            CarManager.Instance.UpdateCarNameDisplay();
        }
        else
        {
            // Handle case where car becomes null (if needed)
            _playerInput = null;
        }
    }

    private void OnEnable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPauseStateChanged += HandlePauseState;
        }
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPauseStateChanged -= HandlePauseState;
        }
    }

    private void Start()
    {

        if (IntroMenuCompleted() && mainMenuCanvas != null && !mainMenuCanvas.activeSelf)
        {
            ShowMainMenu();
        }

        InitializeMenu();

        if (mainMenuCanvas != null) mainMenuCanvas.SetActive(true);
        if (ticketFolder != null) ticketFolder.SetActive(false);

        if (IntroMenuCompleted() && _activeCar == null)
        {
            FindActiveCar();
        }

        InitializeGameManager();
        InitializeAudio();
        InitializeStreet();
        GameManager.Instance.PauseGame();

        if (clockUI != null) clockUI.ShowClock();
        GameManager.Instance.GetComponent<MoneySystem>().SetUIVisible(true);
        GameManager.Instance.GetComponent<XPSystem>().SetUIVisible(true);

        if (freeRoamButton != null)
        {
            _originalFreeRoamButtonColors = freeRoamButton.colors;
            freeRoamButton.onClick.AddListener(OnFreeRoamClicked);
        }

        if (uberButton != null)
        {
            _originalUberButtonColors = uberButton.colors;
            uberButton.onClick.AddListener(OnUberClicked);
        }

        if (exitButton != null)
        {
            exitButton.onClick.AddListener(OnExitButtonClicked);
        }

        if (newJobButton != null)
        {
            newJobButton.onClick.AddListener(OnNewJobButtonClicked);
        }

        _rideRequestGenerator = FindFirstObjectByType<RideRequestGenerator>();

        if (_rideRequestGenerator != null)
        {
            _rideRequestGenerator.SetRequestUIVisible(false);
        }
    }

    public void InitializeMenu()
    {
        if (mainMenuCanvas != null) mainMenuCanvas.SetActive(true);
        if (ticketFolder != null) ticketFolder.SetActive(false);

        InitializeGameManager();
        InitializeAudio();
        InitializeStreet();
        GameManager.Instance.PauseGame();

        if (clockUI != null) clockUI.ShowClock();
        GameManager.Instance.GetComponent<MoneySystem>().SetUIVisible(true);
        GameManager.Instance.GetComponent<XPSystem>().SetUIVisible(true);

        if (freeRoamButton != null)
        {
            _originalFreeRoamButtonColors = freeRoamButton.colors;
            freeRoamButton.onClick.AddListener(OnFreeRoamClicked);
        }

        if (uberButton != null)
        {
            _originalUberButtonColors = uberButton.colors;
            uberButton.onClick.AddListener(OnUberClicked);
        }

        if (exitButton != null)
        {
            exitButton.onClick.AddListener(OnExitButtonClicked);
        }

        if (newJobButton != null)
        {
            newJobButton.onClick.AddListener(OnNewJobButtonClicked);
        }

        _rideRequestGenerator = FindFirstObjectByType<RideRequestGenerator>();

        if (_rideRequestGenerator != null)
        {
            _rideRequestGenerator.SetRequestUIVisible(false);
        }
    }

    public void InitializeStreet()
    {

        if (PlayerPrefs.HasKey(GameConstants.CAR_STREET))
        {
            string savedStreet = PlayerPrefs.GetString(GameConstants.CAR_STREET);
            GameManager.Instance.SetCurrentStreet(savedStreet);
        }

        else
        {
            GameManager.Instance.SetCurrentStreet("Strada Del Lauro");

        }
    }

    private void HandlePauseState(bool isPaused)
    {
        // Don't show main menu if food UI is active
        if (PlayerFoodInteraction.Instance != null && PlayerFoodInteraction.Instance.IsUIActive)
            return;

        if (PlayerCarwashInteraction.Instance?.IsUIActive == true ||
            PlayerInsuranceInteraction.Instance?.IsUIActive == true ||
            PlayerCarDealerInteraction.Instance?.IsUIActive == true ||
            PlayerFuelingInteraction.Instance?.IsUIActive == true)
            
        {
            if (mainMenuCanvas != null) mainMenuCanvas.SetActive(false);
            return;
        }

        if (isPaused)
        {
            if (mainMenuCanvas != null)
            {
                mainMenuCanvas.SetActive(true);
            }
            ResetAllButtonTextColors();
            StopCarMovement();

            if (clockUI != null) clockUI.ShowClock();
            GameManager.Instance.GetComponent<MoneySystem>().SetUIVisible(true);
            GameManager.Instance.GetComponent<XPSystem>().SetUIVisible(true);
        }
        else
        {
            if (mainMenuCanvas != null)
            {
                mainMenuCanvas.SetActive(false);
            }
        }
    }

    public void ShowMainMenu()
    {
        // NEW: Ensure all necessary components are initialized
        InitializeMenu();
        InitializeStreet();

        GameManager.Instance.PauseGame();
        mainMenuCanvas.gameObject.SetActive(true);

        // NEW: Reset button states
        ResetAllButtonTextColors();
        SetAllMenuButtonsActive(true);
    }

    private void SetMainMenuButtonsActive(bool active)
    {
        if (freeRoamButton != null) freeRoamButton.gameObject.SetActive(active);
        if (uberButton != null) uberButton.gameObject.SetActive(active);
        if (noButton != null) noButton.gameObject.SetActive(active);
        if (yesButton != null) yesButton.gameObject.SetActive(active);
    }

    private void SetMainMenuButtonsInteractable(bool interactable)
    {
        if (freeRoamButton != null) freeRoamButton.interactable = interactable;
        if (uberButton != null) uberButton.interactable = interactable;
        if (noButton != null) noButton.interactable = interactable;
        if (yesButton != null) yesButton.interactable = interactable;
    }

    private void InitializeGameManager()
    {
        if (GameManager.Instance == null)
        {
            GameObject gm = new GameObject("GameManager");
            gm.AddComponent<GameManager>();
            gm.AddComponent<TimeSystem>();
        }
    }

    private void InitializeAudio()
    {
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 0;
    }

    private void FindActiveCar()
    {
        CarSpawner spawner = FindFirstObjectByType<CarSpawner>();
        if (spawner != null && spawner.CurrentDriveCar != null)
        {
            _activeCar = spawner.CurrentDriveCar.gameObject;
        }
        else
        {
            DriveCar sceneCar = FindFirstObjectByType<DriveCar>();
            _activeCar = sceneCar != null ? sceneCar.gameObject : null;

            if (_activeCar == null)
            {
                Debug.LogError("No active car found in scene!");
                return;
            }
        }

        // Add true to suppress the car changed event
        GameManager.Instance.SetCurrentCar(_activeCar, true);
    }

    private void StopCarMovement()
    {
        if (_activeCar == null) return;

        foreach (Rigidbody2D wheel in _activeCar.GetComponentsInChildren<Rigidbody2D>())
        {
            wheel.linearVelocity = Vector2.zero;
            wheel.angularVelocity = 0f;
        }

        DriveCar driveCar = _activeCar.GetComponentInChildren<DriveCar>();
        if (driveCar != null)
        {
            driveCar.enabled = false;
        }
    }

    public void OnFreeRoamClicked()
    {
        if (_isTransitioning) return;

        PlaySound(buttonClickSound);
        _isTransitioning = true;

        // Cancel any active Uber job and hide its UI
        UberJob uberJob = FindFirstObjectByType<UberJob>();
        if (uberJob != null)
        {
            if (uberJob.IsJobActive)
            {
                uberJob.CancelJob();
            }
            uberJob.HideJobUI(); // Ensure all job UI is hidden

            // Restore standard UI elements (exclude pickup instructions)
            if (uberJob.JobPanel != null) uberJob.JobPanel.SetActive(true);
            if (uberJob.ReceiptCrText != null) uberJob.ReceiptCrText.gameObject.SetActive(true);
            if (uberJob.ReceiptXpText != null) uberJob.ReceiptXpText.gameObject.SetActive(true);
            if (uberJob.EarningsText != null) uberJob.EarningsText.gameObject.SetActive(true);
            if (uberJob.BrandLoyaltyBonusText != null) uberJob.BrandLoyaltyBonusText.gameObject.SetActive(true);
            if (uberJob.XPEarnedText != null) uberJob.XPEarnedText.gameObject.SetActive(true);
            if (uberJob.TotalCrText != null) uberJob.TotalCrText.gameObject.SetActive(true);
            if (uberJob.TotalXpText != null) uberJob.TotalXpText.gameObject.SetActive(true);
        }

        PoliceSystem policeSystem = FindFirstObjectByType<PoliceSystem>();
        if (policeSystem != null)
        {
            policeSystem.LoadSpeedLimit();
            StartCoroutine(UpdateSpeedLimitAfterFrame(policeSystem));
        }

        RemoveAllPolice();

        GameManager.Instance.GetComponent<MoneySystem>().SetUIVisible(true);
        GameManager.Instance.GetComponent<XPSystem>().SetUIVisible(true);

        RPMMeter.gameObject.SetActive(true);
        GameUI.gameObject.SetActive(true);


        if (clockUI != null) clockUI.ShowClock();

        Odometer odometer = FindFirstObjectByType<Odometer>();
        if (odometer != null)
        {
            freeRoamStartOdometer = odometer.TotalDistanceKM;
            isFreeRoamActive = true;
        }

        policeSpawner.SpawnPoliceOnRandomStreet();

        // Reset button colors
        DirectTextColorChange uberColorChanger = uberButton.GetComponent<DirectTextColorChange>();
        if (uberColorChanger != null && uberButton.gameObject.activeSelf)
        {
            uberColorChanger.ResetToOriginalColor();
        }

        DirectTextColorChange freeRoamColorChanger = freeRoamButton.GetComponent<DirectTextColorChange>();
        if (freeRoamColorChanger != null && freeRoamButton.gameObject.activeSelf)
        {
            freeRoamColorChanger.SetTextColor(true);
        }

        if (freeRoamButton != null)
        {
            freeRoamButton.interactable = false;
        }

        Cursor.visible = false;

        StartCoroutine(StartFreeRoamAfterDelay(freeRoamColorChanger));
    }


    private IEnumerator UpdateSpeedLimitAfterFrame(PoliceSystem policeSystem)
    {
        yield return null; // Wait one frame

        // Now get the current speed limit after it's been loaded
        float currentSpeed = policeSystem.GetCurrentSpeedLimit();

        if (_speedLimitDisplayManager != null)
        {
            _speedLimitDisplayManager.UpdateSpeedLimitDisplay(currentSpeed);
        }

        // Get the freeRoamColorChanger as in original code
        DirectTextColorChange freeRoamColorChanger = freeRoamButton.GetComponent<DirectTextColorChange>();
        StartCoroutine(StartFreeRoamAfterDelay(freeRoamColorChanger));
    }

    private void RemoveAllPolice()
    {
        // First add a "Police" tag to your policePrefab in the Unity editor
        GameObject[] policeObjects = GameObject.FindGameObjectsWithTag("Police");
        foreach (GameObject police in policeObjects)
        {
            Destroy(police);
        }
    }

    private IEnumerator StartFreeRoamAfterDelay(DirectTextColorChange freeRoamColorChanger)
    {
        yield return new WaitForSecondsRealtime(1f);

        if (_playerInput != null)
        {
            _playerInput.actions.FindAction("Eat").Enable();
        }

        GameManager.Instance.ResumeGame();

        if (_activeCar != null)
        {
            DriveCar driveCar = _activeCar.GetComponentInChildren<DriveCar>();
            if (driveCar != null)
            {
                driveCar.enabled = true;
                driveCar.InitializeCarComponents(); // This ensures gear text is initialized
            }
        }

        // Show speedometer and odometer text
        if (speedometerText != null) speedometerText.gameObject.SetActive(true);
        if (odometerText != null) odometerText.gameObject.SetActive(true);

        if (freeRoamButton != null && freeRoamButton.gameObject.activeSelf)
        {
            freeRoamButton.interactable = true;
            if (freeRoamColorChanger != null)
            {
                freeRoamColorChanger.ResetToOriginalColor();
            }
        }

        _isTransitioning = false;
    }

    public void TrackFreeRoamDistance()
    {
        if (!isFreeRoamActive) return;

        Odometer odometer = FindFirstObjectByType<Odometer>();
        CarStats carStats = _activeCar?.GetComponent<CarStats>();
        UberJob uberJob = FindFirstObjectByType<UberJob>();

        if (odometer != null && carStats != null && uberJob != null)
        {
            float freeRoamDistance = odometer.TotalDistanceKM - freeRoamStartOdometer;
            if (freeRoamDistance > 0)
            {
                uberJob.TrackDistanceByBrandAndCountry(freeRoamDistance, carStats);
            }
        }

        isFreeRoamActive = false;
    }

    public void OnUberClicked()
    {
        if (_isTransitioning) return;

        PlaySound(buttonClickSound);
        _isTransitioning = true;

        // Get the PoliceSystem instance
        PoliceSystem policeSystem = FindFirstObjectByType<PoliceSystem>();
        float initialSpeed = policeSystem != null ? policeSystem.GetInitialMaxSpeed() : 50f;

        // Activate speed limit display (only need to call this once)
        if (_speedLimitDisplayManager != null)
        {
            _speedLimitDisplayManager.UpdateSpeedLimitDisplay(initialSpeed);
        }

        DirectTextColorChange uberColorChanger = uberButton.GetComponent<DirectTextColorChange>();
        if (uberColorChanger != null)
        {
            uberColorChanger.SetTextColor(true);
        }

        if (uberButton != null)
        {
            uberButton.interactable = false;
        }

        RPMMeter.gameObject.SetActive(true);
        GameUI.gameObject.SetActive(true);



        StartCoroutine(StartUberModeAfterDelay(uberColorChanger));
    }

    private IEnumerator StartUberModeAfterDelay(DirectTextColorChange uberColorChanger)
    {
        if (_rideRequestGenerator == null)
        {
            _rideRequestGenerator = FindFirstObjectByType<RideRequestGenerator>();
            if (_rideRequestGenerator == null)
            {
                if (uberColorChanger != null) uberColorChanger.ResetToOriginalColor();
                if (uberButton != null) uberButton.interactable = true;
                _isTransitioning = false;
                yield break;
            }
        }

        _rideRequestGenerator.GenerateNewRequest();
        _rideRequestGenerator.SetRequestUIVisible(true);

        yield return null;

        if (uberButton != null)
        {
            uberButton.interactable = true;
        }
        _isTransitioning = false;
    }

    public void OnYesClicked()
    {
        if (_isTransitioning || _rideRequestGenerator == null) return;

        RemoveAllPolice();

        CarRarity carRarity = FindFirstObjectByType<CarRarity>();
        if (carRarity != null)
        {
            carRarity.CalculateCurrentCarRarity();
        }

        DirectTextColorChange yesButtonColorChanger = yesButton.GetComponent<DirectTextColorChange>();
        if (yesButtonColorChanger != null)
        {
            yesButtonColorChanger.SetTextColor(true);
        }

        SetAllMenuButtonsActive(false);

        Cursor.visible = false;

        policeSpawner.SpawnPoliceOnRandomStreet();


        StartCoroutine(StartUberJobAfterDelay(yesButtonColorChanger));
    }

    private IEnumerator StartUberJobAfterDelay(DirectTextColorChange yesButtonColorChanger)
    {
        yield return new WaitForSecondsRealtime(1f);

        if (yesButtonColorChanger != null)
        {
            yesButtonColorChanger.ResetToOriginalColor();
        }

        _rideRequestGenerator.SetRequestUIVisible(false);

        if (clockUI != null)
        {
            clockUI.ShowClock();
        }

        if (_playerInput != null)
        {
            _playerInput.actions.FindAction("Eat").Enable();
        }

        GameManager.Instance.ResumeGame();
        if (_activeCar != null)
        {
            DriveCar driveCar = _activeCar.GetComponentInChildren<DriveCar>();
            if (driveCar != null)
            {
                driveCar.enabled = true;
                driveCar.InitializeCarComponents(); // This ensures gear text is initialized
            }
        }

        UberJob uberJob = FindFirstObjectByType<UberJob>();
        if (uberJob != null)
        {
            uberJob.StartJob(
                _rideRequestGenerator.CurrentPickupHouse,
                _rideRequestGenerator.CurrentDestinationHouse
            );
        }

        GameManager.Instance.GetComponent<MoneySystem>().SetUIVisible(true);
        GameManager.Instance.GetComponent<XPSystem>().SetUIVisible(true);

        ResetButtonTextColor(yesButton);
        SetAllMenuButtonsActive(true);
        _isTransitioning = false;
    }

    private void SetAllMenuButtonsActive(bool active)
    {
        if (freeRoamButton != null) freeRoamButton.interactable = active;
        if (uberButton != null) uberButton.interactable = active;
        if (yesButton != null) yesButton.interactable = active;
        if (noButton != null) noButton.interactable = active;
    }

    public void OnNoClicked()
    {
        if (_isTransitioning || noButton == null || _rideRequestGenerator == null) return;

        _isTransitioning = true;
        PlaySound(buttonClickSound);

        _rideRequestGenerator.yesButton.interactable = false;
        noButton.interactable = false;

        DirectTextColorChange noButtonColorChanger = noButton.GetComponent<DirectTextColorChange>();
        if (noButtonColorChanger != null)
        {
            noButtonColorChanger.SetTextColor(true);
        }

        StartCoroutine(HandleNoButtonResponse(noButtonColorChanger));
    }

    private IEnumerator HandleNoButtonResponse(DirectTextColorChange noButtonColorChanger)
    {
        yield return new WaitForSecondsRealtime(1f);

        if (noButtonColorChanger != null)
        {
            noButtonColorChanger.ResetToOriginalColor();
        }

        _rideRequestGenerator.GenerateNewRequest();

        _rideRequestGenerator.yesButton.interactable = true;
        noButton.interactable = true;

        _isTransitioning = false;
    }

    public void ReturnToMenu()
    {
        PoliceSystem policeSystem = FindFirstObjectByType<PoliceSystem>();
        if (policeSystem != null)
        {
            policeSystem.SaveCurrentSpeedLimit();
        }


        TrackFreeRoamDistance();
        PlaySound(menuCloseSound);

        if (_playerInput != null)
        {
            _playerInput.actions.FindAction("Eat").Disable();
        }

        if (ticketFolder != null) ticketFolder.SetActive(false);

        UberJob uberJob = FindFirstObjectByType<UberJob>();
        if (uberJob != null)
        {
            uberJob.HideJobUI();
            if (uberJob.IsJobActive)
            {
                uberJob.CancelJob();
            }
        }

        if (freeRoamButton != null) freeRoamButton.gameObject.SetActive(true);
        if (uberButton != null) uberButton.gameObject.SetActive(true);
        if (exitButton != null) exitButton.gameObject.SetActive(false);
        if (newJobButton != null) newJobButton.gameObject.SetActive(false);

        if (clockUI != null) clockUI.HideClock();

        RideRequestGenerator rideRequestGenerator = FindFirstObjectByType<RideRequestGenerator>();
        if (rideRequestGenerator != null)
        {
            rideRequestGenerator.SetRequestUIVisible(false);
        }

        XPSystem xpSystem = GameManager.Instance.GetComponent<XPSystem>();
        if (xpSystem != null)
        {
            xpSystem.SetUIVisible(true);
            xpSystem.ForceUpdateXPDisplay();
        }

        GameManager.Instance.GetComponent<MoneySystem>().SetUIVisible(true);

        StopCarMovement();
        ResetAllButtonTextColors();
        GameManager.Instance.PauseGame();

    }

    private void OnExitButtonClicked()
    {
        if (_isTransitioning) return;

        ResetButtonTextColor(yesButton);

        PlaySound(buttonClickSound);
        _isTransitioning = true;

        if (ticketFolder != null) ticketFolder.SetActive(false);
        if (exitButton != null) exitButton.gameObject.SetActive(false);
        if (newJobButton != null) newJobButton.gameObject.SetActive(false);

        // Ensure all UI elements are properly reset
        UberJob uberJob = FindFirstObjectByType<UberJob>();
        if (uberJob != null)
        {
            uberJob.HideJobUI();
        }

        // Show the main menu buttons
        if (freeRoamButton != null)
        {
            freeRoamButton.gameObject.SetActive(true);
            freeRoamButton.interactable = true;
        }
        if (uberButton != null)
        {
            uberButton.gameObject.SetActive(true);
            uberButton.interactable = true;
        }
        if (yesButton != null) yesButton.gameObject.SetActive(true);
        if (noButton != null) noButton.gameObject.SetActive(true);

        // Make sure XP and money UI are visible
        GameManager.Instance.GetComponent<MoneySystem>().SetUIVisible(true);
        GameManager.Instance.GetComponent<XPSystem>().SetUIVisible(true);

        if (clockUI != null) clockUI.ShowClock();

        _isTransitioning = false;
    }

    private void OnNewJobButtonClicked()
    {
        if (_isTransitioning) return;

        PlaySound(buttonClickSound);
        _isTransitioning = true;

        ResetAllButtonTextColors();

        if (ticketFolder != null)
        {
            ticketFolder.SetActive(false);
            if (exitButton != null) exitButton.gameObject.SetActive(false);
            if (newJobButton != null) newJobButton.gameObject.SetActive(false);
        }

        if (freeRoamButton != null)
        {
            freeRoamButton.interactable = true;
            freeRoamButton.gameObject.SetActive(true);
        }
        if (uberButton != null)
        {
            uberButton.interactable = true;
            uberButton.gameObject.SetActive(true);

            var uberColorChanger = uberButton.GetComponent<DirectTextColorChange>();
            if (uberColorChanger != null)
            {
                uberColorChanger.SetTextColor(true);
            }
        }
        if (yesButton != null) yesButton.gameObject.SetActive(true);
        if (noButton != null) noButton.gameObject.SetActive(true);

        StartCoroutine(StartNewJobAfterDelay());
    }

    private IEnumerator StartNewJobAfterDelay()
    {
        yield return new WaitForSecondsRealtime(0.1f);

        if (_rideRequestGenerator == null)
        {
            _rideRequestGenerator = FindFirstObjectByType<RideRequestGenerator>();
        }

        if (_rideRequestGenerator != null)
        {
            _rideRequestGenerator.GenerateNewRequest();
            _rideRequestGenerator.SetRequestUIVisible(true);

            if (uberButton != null)
            {
                var uberColorChanger = uberButton.GetComponent<DirectTextColorChange>();
                if (uberColorChanger != null)
                {
                    uberColorChanger.SetTextColor(true);
                }
            }
        }

        _isTransitioning = false;
    }

    public void ReturnFromCompletion()
    {
        if (speedometerText != null) speedometerText.gameObject.SetActive(true);
        if (odometerText != null) odometerText.gameObject.SetActive(true);

        // Reset button states
        if (uberButton != null)
        {
            var uberColorChanger = uberButton.GetComponent<DirectTextColorChange>();
            if (uberColorChanger != null)
            {
                uberColorChanger.ForceResetToWhite();
            }
            uberButton.interactable = true;
        }

        if (yesButton != null)
        {
            var yesColorChanger = yesButton.GetComponent<DirectTextColorChange>();
            if (yesColorChanger != null)
            {
                yesColorChanger.ForceResetToWhite();
            }
            yesButton.interactable = true;
            yesButton.gameObject.SetActive(true);
        }

        // Hide completion UI
        if (ticketFolder != null) ticketFolder.SetActive(false);
        if (exitButton != null) exitButton.gameObject.SetActive(false);
        if (newJobButton != null) newJobButton.gameObject.SetActive(false);

        // Show main menu buttons
        if (freeRoamButton != null)
        {
            freeRoamButton.interactable = true;
            freeRoamButton.gameObject.SetActive(true);
        }
        if (uberButton != null)
        {
            uberButton.interactable = true;
            uberButton.gameObject.SetActive(true);
        }
        if (noButton != null) noButton.gameObject.SetActive(true);

        // Hide any remaining job UI
        UberJob uberJob = FindFirstObjectByType<UberJob>();
        if (uberJob != null)
        {
            uberJob.HideJobUI();
        }

        // Show all standard HUD elements (same as Free Roam)
        GameManager.Instance.GetComponent<MoneySystem>().SetUIVisible(true);
        GameManager.Instance.GetComponent<XPSystem>().SetUIVisible(true);

        if (clockUI != null) clockUI.ShowClock();

        // Show main menu
        if (mainMenuCanvas != null) mainMenuCanvas.SetActive(true);
    }

    public void ShowJobCompletionUI()
    {
        if (speedometerText != null) speedometerText.gameObject.SetActive(false);
        if (odometerText != null) odometerText.gameObject.SetActive(false);

        if (uberButton != null)
        {
            var uberColorChanger = uberButton.GetComponent<DirectTextColorChange>();
            if (uberColorChanger != null)
            {
                uberColorChanger.ForceResetToWhite();
            }
        }

        if (yesButton != null)
        {
            var yesColorChanger = yesButton.GetComponent<DirectTextColorChange>();
            if (yesColorChanger != null)
            {
                yesColorChanger.ForceResetToWhite();
            }
        }

        if (mainMenuCanvas != null) mainMenuCanvas.SetActive(true);

        if (freeRoamButton != null)
        {
            freeRoamButton.interactable = false;
            freeRoamButton.gameObject.SetActive(true);
        }
        if (uberButton != null)
        {
            uberButton.interactable = false;
            uberButton.gameObject.SetActive(true);
        }
        if (noButton != null) noButton.gameObject.SetActive(false);
        if (yesButton != null) yesButton.gameObject.SetActive(false);

        if (ticketFolder != null)
        {
            ticketFolder.SetActive(true);
            if (exitButton != null) exitButton.gameObject.SetActive(true);
            if (newJobButton != null) newJobButton.gameObject.SetActive(true);
        }

        UberJob uberJob = FindFirstObjectByType<UberJob>();
        if (uberJob != null)
        {
            if (uberJob.ReceiptCrText != null) uberJob.ReceiptCrText.gameObject.SetActive(true);
            if (uberJob.ReceiptXpText != null) uberJob.ReceiptXpText.gameObject.SetActive(true);
            if (uberJob.EarningsText != null) uberJob.EarningsText.gameObject.SetActive(true);
            if (uberJob.BrandLoyaltyBonusText != null) uberJob.BrandLoyaltyBonusText.gameObject.SetActive(true);
            if (uberJob.XPEarnedText != null) uberJob.XPEarnedText.gameObject.SetActive(true);
            if (uberJob.JobPanel != null) uberJob.JobPanel.SetActive(true);
        }

        if (clockUI != null) clockUI.HideClock();

        GameManager.Instance.PauseGame();
    }

    private void ResetButtonTextColor(Button button)
    {
        if (button != null)
        {
            var colorChanger = button.GetComponent<DirectTextColorChange>();
            if (colorChanger != null)
            {
                colorChanger.ResetToOriginalColor();
            }
        }
    }

    public void ResetAllButtonTextColors()
    {
        ResetButtonTextColor(freeRoamButton);
        ResetButtonTextColor(uberButton);
        ResetButtonTextColor(noButton);
        ResetButtonTextColor(yesButton);
    }

    public void OnSettingsClicked()
    {
        PlaySound(buttonClickSound);
    }

    public void QuitGame()
    {
        PlaySound(buttonClickSound);

        // Save game data before quitting
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SaveGame();
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
    Application.Quit();
#endif
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }
}