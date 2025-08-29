using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.UI;

public class IntroMenuController : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TMP_Text whatIsYourNameText;
    [SerializeField] private TMP_InputField nameInputField;
    [SerializeField] private TMP_Text inputtedNameText;
    [SerializeField] private TMP_Text chooseBrandText;
    [SerializeField] private TMP_Text whatCarGrandmaDrivesText;

    [SerializeField] private GameObject introMenuUIContainer;

    [SerializeField] private GameObject FoodUI;

    [Header("Buttons")]
    [SerializeField] private Button[] brandButtons;

    [Header("Settings")]
    [SerializeField] private float initialDelay = 2f;
    [SerializeField] private float postNameDelay = 2f;

    [Header("New Buttons - Grandma Car Selection")]
    [SerializeField] private Button[] grandmaCarButtons;

    private string playerName;
    private string playerFavoriteBrand;
    private bool isIntroCompleted = false;

    private void Awake()
    {
        // Check if intro menu has already been completed
        if (IsIntroMenuCompleted())
        {
            isIntroCompleted = true;
            DisableAllIntroElementsImmediately();
            SpawnCarForCompletedIntro();
            Destroy(this); // Remove this component only
            return;
        }
    }

    private void Start()
    {
        if (isIntroCompleted) return;
        InitializeIntroMenu();
    }

    private void SpawnCarForCompletedIntro()
    {
        // Get the current car ID from saved data
        string currentCarId = PlayerPrefs.GetString(GameConstants.CURRENT_CAR, "");

        if (!string.IsNullOrEmpty(currentCarId))
        {
            CarManager carManager = FindFirstObjectByType<CarManager>();
            CarSpawner carSpawner = FindFirstObjectByType<CarSpawner>();
            GameManager gameManager = FindFirstObjectByType<GameManager>();

            if (carManager != null && carSpawner != null && gameManager != null)
            {
                // Set the current car in CarManager
                carManager.SetCurrentCar(currentCarId);

                // Spawn the car and get the instance
                GameObject carPrefab = carManager.GetCarPrefabById(currentCarId);
                if (carPrefab != null)
                {
                    GameObject carInstance = carSpawner.SpawnCar(carPrefab);
                    gameManager.SetCurrentCar(carInstance);

                    RPMMeter rpmMeter = FindFirstObjectByType<RPMMeter>();
                    if (rpmMeter != null)
                    {
                        rpmMeter.ConnectToCar(carInstance.GetComponentInChildren<DriveCar>());
                    }
                }
            }
        }
    }

    private void DisableAllIntroElementsImmediately()
    {
        // Disable all UI elements
        if (whatIsYourNameText != null) whatIsYourNameText.gameObject.SetActive(false);
        if (nameInputField != null) nameInputField.gameObject.SetActive(false);
        if (inputtedNameText != null) inputtedNameText.gameObject.SetActive(false);
        if (chooseBrandText != null) chooseBrandText.gameObject.SetActive(false);
        if (whatCarGrandmaDrivesText != null) whatCarGrandmaDrivesText.gameObject.SetActive(false);

        // Disable all buttons
        foreach (Button button in brandButtons)
        {
            if (button != null) button.gameObject.SetActive(false);
        }

        foreach (Button button in grandmaCarButtons)
        {
            if (button != null) button.gameObject.SetActive(false);
        }

        // Disable the main container
        if (introMenuUIContainer != null)
        {
            introMenuUIContainer.SetActive(false);
        }
    }

    private void InitializeIntroMenu()
    {
        // Validate references
        if (whatIsYourNameText == null || nameInputField == null ||
            inputtedNameText == null || chooseBrandText == null || brandButtons == null)
        {
            Debug.LogError("UI references not assigned!", this);
            return;
        }

        // Hide all elements initially
        whatIsYourNameText.gameObject.SetActive(false);
        nameInputField.gameObject.SetActive(false);
        inputtedNameText.gameObject.SetActive(false);
        chooseBrandText.gameObject.SetActive(false);

        // Set all brand buttons inactive at the start
        foreach (Button button in brandButtons)
        {
            button.gameObject.SetActive(false);
        }

        foreach (Button button in grandmaCarButtons)
        {
            button.gameObject.SetActive(false);
        }

        StartCoroutine(ShowIntroSequence());
    }

    private bool IsIntroMenuCompleted()
    {
        return PlayerPrefs.GetInt(GameConstants.INTRO_MENU_COMPLETED, 0) == 1;
    }

    private void CompleteIntroMenu()
    {
        PlayerPrefs.SetInt(GameConstants.INTRO_MENU_COMPLETED, 1);
        PlayerPrefs.Save();
    }

    private IEnumerator ShowIntroSequence()
    {
        yield return new WaitForSecondsRealtime(initialDelay);

        whatIsYourNameText.gameObject.SetActive(true);
        nameInputField.gameObject.SetActive(true);
        nameInputField.Select();
        nameInputField.ActivateInputField();

        // Listen for both "Submit" (Enter key) and "End Edit" (focus lost)
        nameInputField.onSubmit.AddListener(OnNameSubmitted);
        nameInputField.onEndEdit.AddListener(OnNameEndEdit);
    }

    // Handles submission via Enter key
    private void OnNameSubmitted(string name)
    {
        ProcessNameSubmission(name);
    }

    // Handles submission when focus is lost (e.g., clicking elsewhere)
    private void OnNameEndEdit(string name)
    {
        // Only process if the player actually typed something
        if (!string.IsNullOrEmpty(name))
        {
            ProcessNameSubmission(name);
        }
    }

    // Common logic for name submission
    private void ProcessNameSubmission(string name)
    {
        // Prevent duplicate processing
        nameInputField.onSubmit.RemoveListener(OnNameSubmitted);
        nameInputField.onEndEdit.RemoveListener(OnNameEndEdit);

        nameInputField.gameObject.SetActive(false);
        inputtedNameText.gameObject.SetActive(true);
        inputtedNameText.text = name;

        playerName = name;
        inputtedNameText.text = name;

        StartCoroutine(ShowBrandSelection());
    }

    private IEnumerator ShowBrandSelection()
    {
        yield return new WaitForSecondsRealtime(postNameDelay);

        chooseBrandText.gameObject.SetActive(true);

        // Activate all brand buttons
        foreach (Button button in brandButtons)
        {
            button.gameObject.SetActive(true);
            button.onClick.AddListener(() => OnBrandButtonClicked(button));
        }
    }

    private void OnBrandButtonClicked(Button clickedButton)
    {
        foreach (Button button in brandButtons)
        {
            // Remove listeners for all buttons
            button.onClick.RemoveAllListeners();
            if (button != clickedButton)
            {
                // Force-disable non-clicked buttons (resets their visuals)
                button.interactable = false;
            }
        }

        playerFavoriteBrand = clickedButton.name;
        StartCoroutine(ShowGrandmaCarText());
    }

    private IEnumerator ShowGrandmaCarText()
    {
        yield return new WaitForSecondsRealtime(initialDelay);

        whatCarGrandmaDrivesText.gameObject.SetActive(true);

        // Activate the new grandma car buttons
        foreach (Button button in grandmaCarButtons)
        {
            button.gameObject.SetActive(true);
            button.interactable = true;
            // Add your click listeners for these new buttons
            button.onClick.AddListener(() => OnGrandmaCarButtonClicked(button));
        }
    }

    private void OnGrandmaCarButtonClicked(Button clickedButton)
    {
        PlayerPrefs.SetString(GameConstants.PLAYER_NAME, playerName);
        PlayerPrefs.SetString(GameConstants.PLAYER_FAVORITE_BRAND, playerFavoriteBrand);

        // Determine the car index based on button name
        int carIndex = -1;
        string carId = clickedButton.name;

        switch (carId)
        {
            case "1920 Fiat 501 Tourer":
                carIndex = 0;
                break;
            case "1922 Citroen C3 Cloverleaf Tourer":
                carIndex = 1;
                break;
            case "1924 Peugeot 172":
                carIndex = 2;
                break;
            default:
                Debug.LogWarning($"Unknown car button: {carId}");
                carIndex = 0;
                break;
        }

        // Set the default car in CarManager
        CarManager carManager = FindFirstObjectByType<CarManager>();
        if (carManager != null && carManager.GetAllCarIds().Count > carIndex)
        {
            string defaultCarId = carManager.GetAllCarIds()[carIndex];
            carManager.SetCurrentCar(defaultCarId);
            PlayerPrefs.SetString(GameConstants.CURRENT_CAR, defaultCarId);

            // NEW: Set the car as owned by getting the prefab's CarStats
            GameObject carPrefab = carManager.GetCarPrefabById(defaultCarId);
            if (carPrefab != null)
            {
                CarStats carStats = carPrefab.GetComponentInChildren<CarStats>(true);
                if (carStats != null)
                {
                    carStats.SetOwned(true);
                    Debug.Log($"Car {defaultCarId} is now owned: {carStats.isOwned}");
                }
            }

            CarSpawner carSpawner = FindFirstObjectByType<CarSpawner>();
            if (carSpawner != null)
            {
                if (carPrefab != null)
                {
                    carSpawner.SpawnCar(carPrefab);
                }
            }

            GameManager gameManager = FindFirstObjectByType<GameManager>();
            if (gameManager != null)
            {
                if (carPrefab != null)
                {
                    gameManager.SetCurrentCar(carPrefab);
                }
            }

            MainMenuController mainMenuController = FindFirstObjectByType<MainMenuController>();
            if (mainMenuController != null)
            {
                mainMenuController.InitializeMenu();
                mainMenuController.ShowMainMenu(); // Ensure menu is shown
            }
        }

        // Mark intro menu as completed (but don't disable UI yet)
        PlayerPrefs.SetInt(GameConstants.INTRO_MENU_COMPLETED, 1);
        PlayerPrefs.Save();

        // Disable all buttons to prevent further clicks
        foreach (Button button in grandmaCarButtons)
        {
            button.interactable = false;
        }

        // Start coroutine for delayed main menu show
        StartCoroutine(ShowMainMenuAfterDelay());
    }


    private IEnumerator ShowMainMenuAfterDelay()
    {
        // Wait for 2 seconds before showing main menu
        yield return new WaitForSecondsRealtime(2f);

        // Disable all intro UI elements (only when completing, not when loading completed intro)
        DisableAllIntroElementsImmediately();

        // Show the main menu
        if (MainMenuController.Instance != null)
        {
            MainMenuController.Instance.ShowMainMenu();
        }

        FoodUI.gameObject.SetActive(false);




        // Destroy this component to prevent any further execution
        Destroy(this);
    }

}