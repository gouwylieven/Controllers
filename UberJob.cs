using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class UberJob : MonoBehaviour
{
    [Header("Job UI")]
    [SerializeField] private GameObject jobPanel;
    [SerializeField] private TextMeshProUGUI pickupInstructionsText;
    [SerializeField] private TextMeshProUGUI dropoffInstructionsText;
    [SerializeField] private TextMeshProUGUI customerRequestText;
    [SerializeField] private float minHaltSpeedKMH = 2f;
    [SerializeField] private TextMeshProUGUI earningsText;
    [SerializeField] private TextMeshProUGUI brandLoyaltyBonusText;
    [SerializeField] private TextMeshProUGUI xpEarnedText;
    [SerializeField] private TextMeshProUGUI receiptCrText;
    [SerializeField] private TextMeshProUGUI receiptXpText;
    [SerializeField] private MainMenuController mainMenuController;
    [SerializeField] private TextMeshProUGUI totalCrText;
    [SerializeField] private TextMeshProUGUI totalXpText;
    [SerializeField] private TextMeshProUGUI speedometerText;
    [SerializeField] private TextMeshProUGUI odometerDisplay;

    [Header("Friendship System")]
    [SerializeField] private FriendshipSystem friendshipSystem;

    [Header("XP System")]
    [SerializeField] private XPSystem xpSystem;
    [SerializeField] private CarRarity carRarity;

    [Header("Filth System")]
    [SerializeField] private FilthSystem filthSystem;

    [Header("Hunger System")]
    [SerializeField] private TextMeshProUGUI tooHungryText;
    [SerializeField] private HungerSystem hungerSystem;

    private HouseData currentPickup;
    private HouseData currentDestination;
    private bool isPickupReached = false;
    private bool isJobActive = false;
    private bool isWaitingForHorn = false;
    private float customerTip = 0f;

    private DriveCar carController;
    private CollisionDetector collisionDetector;
    private RideRequestGenerator rideRequestGenerator;
    private Speedometer speedometer;
    private HouseData currentHouseInFront;
    private PlayerInput playerInput;
    private MoneySystem moneySystem;
    private Odometer odometer;
    private CarStats carStats;

    private bool friendshipUpdated = false;
    private string currentClientName;
    private bool earningsAdded = false;
    private float odometerAtJobStart;
    private float odometerAtDropoff;
    private float jobDistance;
    private bool isNearTargetHouse;
    private DistanceData _cachedDistanceData;

    private const float SPEED_CHECK_INTERVAL = 0.2f;
    private const float PROXIMITY_CHECK_INTERVAL = 1f;
    private const float UI_DELAY = 0.05f;

    public TextMeshProUGUI TotalCrText => totalCrText;
    public TextMeshProUGUI TotalXpText => totalXpText;
    public TextMeshProUGUI ReceiptCrText => receiptCrText;
    public TextMeshProUGUI XPEarnedText => xpEarnedText;
    public TextMeshProUGUI BrandLoyaltyBonusText => brandLoyaltyBonusText;
    public TextMeshProUGUI EarningsText => earningsText;
    public TextMeshProUGUI ReceiptXpText => receiptXpText;

    public bool IsJobActive => isJobActive;
    public GameObject JobPanel => jobPanel;
    public TextMeshProUGUI PickupInstructionsText => pickupInstructionsText;
    public TextMeshProUGUI DropoffInstructionsText => dropoffInstructionsText;

    [System.Serializable]
    private class DistanceData
    {
        public Dictionary<string, float> distanceByBrand = new Dictionary<string, float>();
        public Dictionary<string, float> distanceByCountry = new Dictionary<string, float>();
    }

    private void Awake()
    {
        InitializeComponents();
        SetupEventListeners();
    }

    public void InitializeComponents()
    {
        carController = FindFirstObjectByType<DriveCar>();
        collisionDetector = FindFirstObjectByType<CollisionDetector>();
        rideRequestGenerator = FindFirstObjectByType<RideRequestGenerator>();
        speedometer = FindFirstObjectByType<Speedometer>();
        playerInput = FindFirstObjectByType<PlayerInput>();
        moneySystem = FindFirstObjectByType<MoneySystem>();
        odometer = FindFirstObjectByType<Odometer>();
        carStats = FindFirstObjectByType<CarStats>();

        if (carRarity == null) carRarity = FindFirstObjectByType<CarRarity>();

        if (rideRequestGenerator != null && rideRequestGenerator.yesButton != null)
        {
            rideRequestGenerator.yesButton.onClick.AddListener(OnRequestAccepted);
        }
    }

    private void SetupEventListeners()
    {
        if (collisionDetector != null)
        {
            collisionDetector.OnHouseEnter += HandleHouseEnter;
            collisionDetector.OnHouseExit += HandleHouseExit;
        }

        if (playerInput != null)
        {
            playerInput.actions["CarControl/Horn"].performed += OnHornPerformed;
        }
    }

    private void OnEnable()
    {
        InvokeRepeating(nameof(ProximityCheck), 0f, PROXIMITY_CHECK_INTERVAL);
    }

    private void OnDisable()
    {
        CancelInvoke(nameof(ProximityCheck));
        CancelInvoke(nameof(CheckSpeedConditions));
    }

    private void OnDestroy()
    {
        CleanupEventListeners();
        CancelInvoke();
    }

    private void CleanupEventListeners()
    {
        if (rideRequestGenerator != null && rideRequestGenerator.yesButton != null)
        {
            rideRequestGenerator.yesButton.onClick.RemoveListener(OnRequestAccepted);
        }

        if (playerInput != null)
        {
            playerInput.actions["CarControl/Horn"].performed -= OnHornPerformed;
        }

        if (collisionDetector != null)
        {
            collisionDetector.OnHouseEnter -= HandleHouseEnter;
            collisionDetector.OnHouseExit -= HandleHouseExit;
        }
    }

    private void ProximityCheck()
    {
        if (!isJobActive || currentHouseInFront == null)
        {
            if (isNearTargetHouse)
            {
                isNearTargetHouse = false;
                CancelInvoke(nameof(CheckSpeedConditions));
            }
            return;
        }

        bool shouldBeNear = (currentHouseInFront == currentPickup && !isPickupReached) ||
                          (currentHouseInFront == currentDestination && isPickupReached);

        if (shouldBeNear && !isNearTargetHouse)
        {
            isNearTargetHouse = true;
            InvokeRepeating(nameof(CheckSpeedConditions), 0f, SPEED_CHECK_INTERVAL);
        }
        else if (!shouldBeNear && isNearTargetHouse)
        {
            isNearTargetHouse = false;
            CancelInvoke(nameof(CheckSpeedConditions));
        }
    }

    private void CheckSpeedConditions()
    {
        if (!isJobActive || currentHouseInFront == null)
        {
            if (!isJobActive) Debug.Log("CheckSpeedConditions: Job not active");
            if (currentHouseInFront == null) Debug.Log("CheckSpeedConditions: No house in front");
            return;
        }

        bool isHalted = IsCarHalted();

        if (!isPickupReached && currentHouseInFront == currentPickup && isHalted)
        {
            isWaitingForHorn = true;
        }
        else if (isPickupReached && currentHouseInFront == currentDestination && isHalted)
        {
            CompleteJob();
        }
    }

    private void OnHornPerformed(InputAction.CallbackContext context)
    {

        if (!isWaitingForHorn)
        {
            return;
        }

        if (!isJobActive)
        {
            return;
        }

        if (isPickupReached)
        {
            return;
        }

        // Check hunger level
        if (hungerSystem != null && hungerSystem.CurrentHunger > 23)
        {
            // Hide pickup instructions before showing too hungry message
            if (pickupInstructionsText != null)
            {
                pickupInstructionsText.gameObject.SetActive(false);
            }

            HandleTooHungry();
            return;
        }

        // Check filth level
        if (filthSystem != null && filthSystem.CurrentFilth > 23)
        {
            // Hide pickup instructions before showing too filthy message
            if (pickupInstructionsText != null)
            {
                pickupInstructionsText.gameObject.SetActive(false);
            }

            HandleTooFilthy();
            return;
        }

        isWaitingForHorn = false;
        OnPickupCompleted();
    }

    private void HandleTooHungry()
    {
        // Show too hungry message
        if (tooHungryText != null)
        {
            tooHungryText.text = $"You are too hungry to be able to do your job.You failed and lost one friendshiplevel with {currentClientName}.";
            tooHungryText.gameObject.SetActive(true);
        }

        // Decrease friendship
        if (friendshipSystem != null && !string.IsNullOrEmpty(currentClientName))
        {
            string key = $"Friendship_{currentClientName}";
            int currentLevel = PlayerPrefs.GetInt(key, 0);
            PlayerPrefs.SetInt(key, currentLevel - 1);
            PlayerPrefs.Save();
        }

        // Cancel the job after delay
        StartCoroutine(CancelJobAfterDelay());
    }

    private void HandleTooFilthy()
    {
        // Show too filthy message
        if (tooHungryText != null)
        {
            tooHungryText.text = $"Your car is too filthy to pick up passengers. You failed and lost one friendshiplevel with {currentClientName}.";
            tooHungryText.gameObject.SetActive(true);
        }

        // Decrease friendship
        if (friendshipSystem != null && !string.IsNullOrEmpty(currentClientName))
        {
            string key = $"Friendship_{currentClientName}";
            int currentLevel = PlayerPrefs.GetInt(key, 0);
            PlayerPrefs.SetInt(key, currentLevel - 1);
            PlayerPrefs.Save();
        }

        // Cancel the job after delay
        StartCoroutine(CancelJobAfterDelay());
    }

    private IEnumerator CancelJobAfterDelay()
    {
        yield return new WaitForSecondsRealtime(4.5f);

        // Hide the message
        if (tooHungryText != null)
        {
            tooHungryText.gameObject.SetActive(false);
        }

        // Return to main menu
        CancelJob();

        if (mainMenuController != null)
        {
            mainMenuController.ReturnToMenu();
        }
    }



    private void OnRequestAccepted()
    {
        currentPickup = rideRequestGenerator.CurrentPickupHouse;
        currentDestination = rideRequestGenerator.CurrentDestinationHouse;
        currentClientName = rideRequestGenerator.CurrentClientInfo.name;

        if (currentPickup == null || currentDestination == null) return;

        StartJob(currentPickup, currentDestination);
    }

    public void StartJob(HouseData pickup, HouseData destination)
    {
        ResetJobState();
        currentPickup = pickup;
        currentDestination = destination;
        isJobActive = true;

        InitializeOdometer();
        SetupJobUI();
    }

    private void ResetJobState()
    {
        friendshipUpdated = false;
        earningsAdded = false;
        isPickupReached = false;
        isWaitingForHorn = false;
        _cachedDistanceData = null;
        customerTip = 0f; // Reset customer tip
        if (tooHungryText != null) tooHungryText.gameObject.SetActive(false);
        if (customerRequestText != null) customerRequestText.gameObject.SetActive(false);
    }

    private void InitializeOdometer()
    {
        if (odometer != null)
        {
            odometerAtJobStart = odometer.TotalDistanceKM;
        }
    }

    private void SetupJobUI()
    {
        var clientInfo = rideRequestGenerator.CurrentClientInfo;
        pickupInstructionsText.text = $"Drive to {currentPickup.address}. Stop and sound the horn to pick up {clientInfo.name} {clientInfo.coriders}.";
        dropoffInstructionsText.text = $"Now drive to {currentDestination.address} to drop off {clientInfo.name} {clientInfo.coriders}.";

        // NEW: Set up customer request text if available
        if (customerRequestText != null)
        {
            // Check if this is the home house and has a request
            bool isHomeHouse = currentDestination.residence?.ToLower().Contains("home") == true ||
                              currentDestination.Name?.ToLower() == "home";

            if (!string.IsNullOrEmpty(currentDestination.request))
            {
                customerRequestText.text = currentDestination.request;
                customerRequestText.gameObject.SetActive(false);
            }
        }

        pickupInstructionsText.gameObject.SetActive(false);
        dropoffInstructionsText.gameObject.SetActive(false);
        jobPanel.SetActive(true);

        if (earningsText != null) earningsText.gameObject.SetActive(false);
        if (brandLoyaltyBonusText != null) brandLoyaltyBonusText.gameObject.SetActive(false);
        if (xpEarnedText != null) xpEarnedText.gameObject.SetActive(false);

        Invoke(nameof(ShowInitialPickupText), UI_DELAY);
    }



    private float CalculateEarnings()
    {
        const float baseFare = 10f;
        int passengers = carStats != null ? carStats.passengers : 1;
        float bonusMultiplier = 1f;

        // Calculate customer tip FIRST
        customerTip = 0f;
        if (!string.IsNullOrEmpty(currentClientName) && customerRequestText != null && customerRequestText.gameObject.activeSelf)
        {
            float baseEarningsWithoutBonus = (baseFare + (jobDistance * 150f)) * passengers;
            customerTip = Mathf.Round(baseEarningsWithoutBonus / (passengers * 2f) * 100f) / 100f;
            Debug.Log($"Customer tip calculated: {customerTip:F2} (Base: {baseEarningsWithoutBonus:F2}, Passengers: {passengers})");
        }

        if (carStats != null && !string.IsNullOrEmpty(carStats.brand))
        {
            string brand = carStats.brand;
            float brandDistance = GetBrandDistance(brand);
            float brandLoyaltyBonus = CalculateBrandLoyaltyBonus(brandDistance);
            bonusMultiplier += brandLoyaltyBonus;

            // Now UpdateBrandLoyaltyUI will have the correct customerTip value
            UpdateBrandLoyaltyUI(brand, brandDistance, brandLoyaltyBonus, baseFare, passengers);
        }

        float distanceEarnings = Mathf.Round(jobDistance * 150f * 100f) / 100f;

        // Calculate base earnings without tip
        float baseEarnings = Mathf.Round((baseFare + distanceEarnings) * passengers * bonusMultiplier * 100f) / 100f;

        // Total earnings = base earnings + customer tip
        float totalEarnings = Mathf.Round((baseEarnings + customerTip) * 100f) / 100f;

        return totalEarnings;
    }

    private void UpdateBrandLoyaltyUI(string brand, float brandDistance, float brandLoyaltyBonus, float baseFare, int passengers)
    {
        float baseEarnings = (baseFare + (jobDistance * 150f)) * passengers;
        float bonusAmount = baseEarnings * brandLoyaltyBonus;
        float earningsWithBonus = baseEarnings + bonusAmount + customerTip;

        brandLoyaltyBonusText.text =
            $"{brand} sponsorship: {brandLoyaltyBonus * 100:F0}% for driving {brandDistance:F1} km in their cars.\n" +
            $"(Forfait 10.00CR + Distance {(jobDistance * 150f):F2}CR) × Passengers {passengers} = {baseEarnings:F2}CR\n" +
            $"+ Sponsorship {bonusAmount:F2}CR + Tip {customerTip:F2}CR = {earningsWithBonus:F2}CR";

        brandLoyaltyBonusText.gameObject.SetActive(true);
    }

    private float GetBrandDistance(string brand)
    {
        var data = LoadDistanceData();
        string brandKey = brand.ToLower().Trim();
        if (data.distanceByBrand.TryGetValue(brandKey, out float distance))
        {
            return distance;
        }
        return 0f;
    }

    private DistanceData LoadDistanceData()
    {
        if (_cachedDistanceData != null) return _cachedDistanceData;

        string json = PlayerPrefs.GetString("DistanceData", "{}");
        if (string.IsNullOrEmpty(json) || json == "{}") return new DistanceData();

        try
        {
            var wrapper = JsonUtility.FromJson<DistanceDataWrapper>(json);
            var data = new DistanceData();

            for (int i = 0; i < Mathf.Min(wrapper.brandKeys.Count, wrapper.brandValues.Count); i++)
                data.distanceByBrand[wrapper.brandKeys[i]] = wrapper.brandValues[i];

            for (int i = 0; i < Mathf.Min(wrapper.countryKeys.Count, wrapper.countryValues.Count); i++)
                data.distanceByCountry[wrapper.countryKeys[i]] = wrapper.countryValues[i];

            return _cachedDistanceData = data;
        }
        catch
        {
            return new DistanceData();
        }
    }

    private void SaveDistanceData(DistanceData data)
    {
        if (data == null) return;

        try
        {
            var wrapper = new DistanceDataWrapper
            {
                brandKeys = data.distanceByBrand.Keys.ToList(),
                brandValues = data.distanceByBrand.Values.ToList(),
                countryKeys = data.distanceByCountry.Keys.ToList(),
                countryValues = data.distanceByCountry.Values.ToList()
            };

            PlayerPrefs.SetString("DistanceData", JsonUtility.ToJson(wrapper));
            PlayerPrefs.Save();
        }
        catch { }
    }

    [System.Serializable]
    private class DistanceDataWrapper
    {
        public List<string> brandKeys = new List<string>();
        public List<float> brandValues = new List<float>();
        public List<string> countryKeys = new List<string>();
        public List<float> countryValues = new List<float>();
    }

    private float CalculateBrandLoyaltyBonus(float brandDistanceDriven)
    {
        if (brandDistanceDriven < 1f)
            return 0.01f;

        if (brandDistanceDriven <= 5f)
            return 0.01f + Mathf.Floor(brandDistanceDriven) * 0.01f;

        float remainingDistance = brandDistanceDriven - 5f;
        int groupNumber = 0;
        float totalBonus = 0.05f;

        while (true)
        {
            int kmPerStep = 3 + groupNumber;
            float bonusPerStep = 0.02f + (0.01f * groupNumber);
            float groupSize = kmPerStep * 5f;

            if (remainingDistance <= groupSize)
            {
                int stepsCompleted = Mathf.FloorToInt(remainingDistance / kmPerStep);
                totalBonus += stepsCompleted * bonusPerStep;
                return Mathf.Min(totalBonus, 10.00f);
            }

            remainingDistance -= groupSize;
            totalBonus += 5f * bonusPerStep;
            groupNumber++;
        }
    }

    public void TrackDistanceByBrandAndCountry(float distance, CarStats carStats)
    {
        if (carStats == null) return;

        DistanceData data = LoadDistanceData();

        if (!string.IsNullOrEmpty(carStats.brand))
        {
            string brandKey = carStats.brand.ToLower().Trim();
            data.distanceByBrand[brandKey] = data.distanceByBrand.TryGetValue(brandKey, out float current) ? current + distance : distance;
        }

        if (!string.IsNullOrEmpty(carStats.country))
        {
            string countryKey = carStats.country.ToLower().Trim();
            data.distanceByCountry[countryKey] = data.distanceByCountry.TryGetValue(countryKey, out float current) ? current + distance : distance;
        }

        SaveDistanceData(data);
    }

    private void ShowInitialPickupText()
    {
        pickupInstructionsText.gameObject.SetActive(true);
        speedometerText.gameObject.SetActive(true);
        odometerDisplay.gameObject.SetActive(true);

    }

    private void HandleHouseEnter(HouseData houseData)
    {
        if (!isJobActive) return;
        currentHouseInFront = houseData;
    }

    private void HandleHouseExit(HouseData houseData)
    {
        if (houseData == currentHouseInFront)
        {
            currentHouseInFront = null;
        }
    }

    private bool IsCarHalted()
    {
        if (speedometer == null) return false;
        return speedometer.CurrentSpeedKMH < minHaltSpeedKMH;
    }

    private void OnPickupCompleted()
    {
        isPickupReached = true;

        // Find the customer's home house (the one with their name)
        HouseData homeHouse = null;

        if (currentPickup != null && currentPickup.Name == currentClientName)
        {
            homeHouse = currentPickup;
            Debug.Log($"Home house is pickup: {homeHouse.address}, Name: {homeHouse.Name}");
        }
        else if (currentDestination != null && currentDestination.Name == currentClientName)
        {
            homeHouse = currentDestination;
            Debug.Log($"Home house is destination: {homeHouse.address}, Name: {homeHouse.Name}");
        }
        else
        {
            Debug.LogWarning($"Could not find home house for customer: {currentClientName}");
            Debug.Log($"Pickup Name: {currentPickup?.Name}, Destination Name: {currentDestination?.Name}");
        }

        if (pickupInstructionsText == null)
        {
            Debug.LogError("pickupInstructionsText is null - might have been destroyed");
            return;
        }

        if (dropoffInstructionsText == null)
        {
            Debug.LogError("dropoffInstructionsText is null - might have been destroyed");
            return;
        }

        if (jobPanel != null && !jobPanel.activeSelf)
        {
            jobPanel.SetActive(true);
        }

        pickupInstructionsText.gameObject.SetActive(false);
        dropoffInstructionsText.gameObject.SetActive(true);

        // Show customer request text if available - check the HOME house's request field
        if (customerRequestText != null && homeHouse != null)
        {
            Debug.Log($"Home house request field: '{homeHouse.request}'");

            if (!string.IsNullOrEmpty(homeHouse.request))
            {
                string request = homeHouse.request.Trim().ToUpper();
                Debug.Log($"Processing request from home house: '{request}' for client: {currentClientName}");

                // Handle special RPM+ request
                if (request == "RPM+")
                {
                    Debug.Log("RPM+ request detected");
                    if (carStats != null)
                    {
                        float targetRPM = 2f / 5f * carStats.maxRpm;
                        string targetRPMFormatted = targetRPM.ToString("F0");
                        Debug.Log($"Target RPM calculated: {targetRPMFormatted} (maxRPM: {carStats.maxRpm})");

                        // List of possible RPM+ messages
                        string[] rpmMessages = new string[]
                        {
                        $"{currentClientName} loves the sound of an engine singing. Keep those revs over {targetRPMFormatted}RPM!",
                        $"Oh and by the way, {currentClientName} is quite adventurous and would love you to stay over {targetRPMFormatted}RPM the whole ride.",
                        $"{currentClientName} is a total speed freak, stay over {targetRPMFormatted}RPM!",
                        $"{currentClientName} lives for the roar of the engine—don't let those revs dip below {targetRPMFormatted}RPM!",
                        $"{targetRPMFormatted}RPM is the baseline. Anything less and {currentClientName} might just jump out and drive himself.",
                        $"{currentClientName} wants thrills, not naps. Keep that needle pinned above {targetRPMFormatted}RPM.",
                        $"If the engine isn't howling, {currentClientName} isn't smiling. Keep it over {targetRPMFormatted}RPM all the way."
                        };

                        // Pick a random message
                        int randomIndex = Random.Range(0, rpmMessages.Length);
                        customerRequestText.text = rpmMessages[randomIndex];
                        Debug.Log($"Selected RPM+ message: {rpmMessages[randomIndex]}");
                    }
                    else
                    {
                        customerRequestText.text = "RPM challenge! Keep those revs high!";
                        Debug.LogWarning("carStats is null for RPM+ request");
                    }
                }
                // Handle special RPM- request
                else if (request == "RPM-")
                {
                    Debug.Log("RPM- request detected");
                    if (carStats != null)
                    {
                        float targetRPM = 3f / 5f * carStats.maxRpm;
                        string targetRPMFormatted = targetRPM.ToString("F0");
                        Debug.Log($"Target RPM calculated: {targetRPMFormatted} (maxRPM: {carStats.maxRpm})");

                        // List of possible RPM- messages
                        string[] rpmMessages = new string[]
                        {
                        $"{currentClientName} prefers rides like a good espresso: smooth, strong, and never rushed. Keep it under {targetRPMFormatted}RPM",
                        $"Let me tell you, {currentClientName} wants to feel safe. Stay under {targetRPMFormatted}RPM the whole ride.",
                        $"{currentClientName} wants to enjoy the ride, not just survive it. Keep those revs under {targetRPMFormatted}RPM.",
                        $"Comfort over chaos. {currentClientName} prefers a composed ride—under {targetRPMFormatted}RPM, always.",
                        $"Trust me, {currentClientName} wants to feel safe and steady. Keep it under {targetRPMFormatted}RPM."
                        };

                        // Pick a random message
                        int randomIndex = Random.Range(0, rpmMessages.Length);
                        customerRequestText.text = rpmMessages[randomIndex];
                        Debug.Log($"Selected RPM- message: {rpmMessages[randomIndex]}");
                    }
                    else
                    {
                        customerRequestText.text = "Smooth ride requested! Keep those revs low!";
                        Debug.LogWarning("carStats is null for RPM- request");
                    }
                }
                else
                {
                    // Regular request text
                    Debug.Log("Regular request detected");
                    customerRequestText.text = homeHouse.request;
                }
                customerRequestText.gameObject.SetActive(true);
            }
            else
            {
                Debug.Log($"Home house found but request field is empty or null: '{homeHouse.request}'");
                customerRequestText.gameObject.SetActive(false);
            }
        }
        else
        {
            Debug.Log($"Cannot show request. CustomerRequestText: {customerRequestText != null}, HomeHouse: {homeHouse != null}");
        }
    }

    private void CompleteJob()
    {
        if (friendshipUpdated || earningsAdded) return;

        PoliceSystem policeSystem = FindFirstObjectByType<PoliceSystem>();
        if (policeSystem != null)
        {
            policeSystem.SaveCurrentSpeedLimit();
        }

        if (odometer != null)
        {
            odometerAtDropoff = odometer.TotalDistanceKM;
            jobDistance = Mathf.Round((odometerAtDropoff - odometerAtJobStart) * 100f) / 100f;
            TrackDistanceByBrandAndCountry(jobDistance, carStats);

            // Process earnings and XP first
            ProcessEarnings(carStats);
            ProcessXP(carStats);
        }

        pickupInstructionsText.gameObject.SetActive(false);
        dropoffInstructionsText.gameObject.SetActive(false);

        if (customerRequestText != null)
        {
            customerRequestText.gameObject.SetActive(false);
        }

        // Then show the UI
        ShowCompletionUI();
        isJobActive = false;
        isNearTargetHouse = false;
        pickupInstructionsText.gameObject.SetActive(false);
        dropoffInstructionsText.gameObject.SetActive(false);
        CancelInvoke(nameof(CheckSpeedConditions));
        UpdateFriendship();
        filthSystem?.IncreaseFilth(1);

        // Start coroutine to wait before showing completion UI
        StartCoroutine(CompleteJobWithDelay());
    }

    private IEnumerator CompleteJobWithDelay()
    {
        // Wait for 1 second before showing the completion UI
        yield return new WaitForSeconds(1f);

        // Show completion UI through MainMenuController
        if (mainMenuController != null)
        {
            mainMenuController.ShowJobCompletionUI();
        }
    }

    public void ReturnToMenuFromCompletion()
    {
        if (mainMenuController != null)
        {
            mainMenuController.ReturnToMenu();
        }
    }

    private float CalculateXP(CarStats carStats)
    {
        if (carStats == null) return 0f;

        int rarity = 1;
        if (carRarity != null)
        {
            carRarity.CalculateCurrentCarRarity();
            rarity = carRarity.GetCurrentCarRarity();
        }

        return Mathf.Round((jobDistance * carStats.carClass * rarity) / 100f * 100f) / 100f;
    }

    private void ProcessXP(CarStats carStats)
    {
        if (carStats == null || xpSystem == null) return;

        float xpEarned = CalculateXP(carStats);
        int rarity = carRarity != null ? carRarity.GetCurrentCarRarity() : 1;

        // Add XP first
        xpSystem.AddXP(xpEarned);

        if (xpEarnedText != null)
        {
            xpEarnedText.text = $"(Distance {jobDistance:F2} * Car Class {carStats.carClass} * Car Rarity {rarity}) / 100 = {xpEarned:F2}XP";
            xpEarnedText.gameObject.SetActive(true);
        }

        if (receiptXpText != null)
        {
            receiptXpText.text = FormatXpReceiptText(jobDistance, carStats.carClass, rarity, xpEarned);
            receiptXpText.gameObject.SetActive(true);
        }

        // Update total XP text with the new value
        if (totalXpText != null)
        {
            totalXpText.text = $"{xpSystem.TotalXP:F2} Total XP";
            totalXpText.gameObject.SetActive(true);
        }
    }


    private string FormatXpReceiptText(float distance, int carClass, int rarity, float totalXp)
    {
        return $"Driven Distance\t{distance:F2} km\n" +
               $"Car Class\t\t{carClass}\n" +
               $"Car Rarity\t\t{rarity}\n\n" +
               $"<b>Earned Rep\t{totalXp:F2}XP</b>";
    }

    public void ShowCompletionUI()
    {
        if (earningsText != null) earningsText.gameObject.SetActive(true);
        if (brandLoyaltyBonusText != null) brandLoyaltyBonusText.gameObject.SetActive(true);
        if (xpEarnedText != null) xpEarnedText.gameObject.SetActive(true);
        jobPanel.SetActive(true);
    }

    private void ProcessEarnings(CarStats carStats)
    {
        float earnings = CalculateEarnings();
        float xpEarned = CalculateXP(carStats);
        int passengers = carStats != null ? carStats.passengers : 1;
        float brandBonus = CalculateBrandLoyaltyBonus(GetBrandDistance(carStats?.brand ?? ""));

        // Add money first
        moneySystem?.AddMoney(earnings);
        earningsAdded = true;

        if (earningsText != null)
        {
            earningsText.text = $"Great job! You earned {earnings:F2}CR and {xpEarned:F2}XP.";
            earningsText.gameObject.SetActive(true);
        }

        if (receiptCrText != null)
        {
            receiptCrText.text = FormatReceiptText(jobDistance, passengers, brandBonus, earnings);
            receiptCrText.gameObject.SetActive(true);
        }

        // Update total CR text with the new value
        if (totalCrText != null && moneySystem != null)
        {
            totalCrText.text = $"{moneySystem.TotalMoney:F2} Total CR";
            totalCrText.gameObject.SetActive(true);
        }
    }


    private string FormatReceiptText(float distance, int passengers, float brandBonus, float totalEarnings)
    {
        return $"Forfait\t\t10.00\n" +
               $"Driven Distance\t{distance:F2} km\n" +
               $"Customers\t\t{passengers}\n" +
               $"Brand Bonus\t{brandBonus:P0}\n" +
               $"Customer Tip\t{customerTip:F2}CR\n\n" +
               $"<b>Earned Credits\t{totalEarnings:F2}CR</b>";
    }

    public void HideJobUI()
    {
        if (jobPanel != null) jobPanel.SetActive(false);
        if (earningsText != null) earningsText.gameObject.SetActive(false);
        if (brandLoyaltyBonusText != null) brandLoyaltyBonusText.gameObject.SetActive(false);
        if (xpEarnedText != null) xpEarnedText.gameObject.SetActive(false);
        if (receiptCrText != null) receiptCrText.gameObject.SetActive(false);
        if (receiptXpText != null) receiptXpText.gameObject.SetActive(false);
        if (tooHungryText != null) tooHungryText.gameObject.SetActive(false);
        if (totalCrText != null) totalCrText.gameObject.SetActive(false);
        if (totalXpText != null) totalXpText.gameObject.SetActive(false);
        if (pickupInstructionsText != null) pickupInstructionsText.gameObject.SetActive(false);
        if (dropoffInstructionsText != null) dropoffInstructionsText.gameObject.SetActive(false);
        if (customerRequestText != null) customerRequestText.gameObject.SetActive(false);

    }

    private void HideCompletionUI()
    {
        if (earningsText != null) earningsText.gameObject.SetActive(false);
        if (brandLoyaltyBonusText != null) brandLoyaltyBonusText.gameObject.SetActive(false);
        if (xpEarnedText != null) xpEarnedText.gameObject.SetActive(false);
        if (receiptCrText != null) receiptCrText.gameObject.SetActive(false);
        if (receiptXpText != null) receiptXpText.gameObject.SetActive(false);
        jobPanel.SetActive(false);
    }

    private void UpdateFriendship()
    {
        if (friendshipSystem != null && !string.IsNullOrEmpty(currentClientName))
        {
            friendshipSystem.IncreaseFriendship(currentClientName);
            friendshipUpdated = true;
        }
    }

    private void GenerateNewRequest()
    {
        rideRequestGenerator?.GenerateNewRequest();
    }

    private void ShowMainMenu()
    {
        if (jobPanel != null) jobPanel.SetActive(false);

        if (mainMenuController != null)
        {
            mainMenuController.ReturnToMenu();
        }
        else
        {
            FindFirstObjectByType<MainMenuController>()?.ReturnToMenu();
        }
    }

    public void CancelJob()
    {
        isJobActive = false;
        isWaitingForHorn = false;
        currentHouseInFront = null;
        isNearTargetHouse = false;

        if (jobPanel != null) jobPanel.SetActive(false);
        if (pickupInstructionsText != null) pickupInstructionsText.gameObject.SetActive(false);
        if (dropoffInstructionsText != null) dropoffInstructionsText.gameObject.SetActive(false);
        if (receiptXpText != null) receiptXpText.gameObject.SetActive(false);

        CancelInvoke(nameof(CheckSpeedConditions));
        CancelInvoke(nameof(HideCompletionUI));
        CancelInvoke(nameof(ShowMainMenu));
    }
}