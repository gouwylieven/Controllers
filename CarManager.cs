using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using TMPro;

[DefaultExecutionOrder(-80)]
public class CarManager : MonoBehaviour
{
    public static CarManager Instance { get; private set; }

    [System.Serializable]
    public class CarData
    {
        public string carId;
        public GameObject carPrefab;
        // We don't store CarStats reference since it's on the prefab
    }

    public bool IsCarOwned(string carId)
    {
        CarStats stats = GetCarStatsById(carId);
        return stats != null && stats.isOwned;
    }

    public List<string> GetOwnedCarIds()
    {
        List<string> ownedCars = new List<string>();
        foreach (var carData in carDatabase)
        {
            if (IsCarOwned(carData.carId))
            {
                ownedCars.Add(carData.carId);
            }
        }
        return ownedCars;
    }

    [Header("Car Database")]
    [SerializeField] private List<CarData> carDatabase = new List<CarData>();

    [Header("Current Car")]
    [SerializeField] private string currentCarId;

    [Header("UI References")]
    [SerializeField] private TMP_Text carNameText; // New field for car name display

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadCurrentCar();
        UpdateCarNameDisplay(); // Update display on startup
    }

    public void AddCarToDatabase(CarData carData)
    {
        if (carDatabase.Any(c => c.carId == carData.carId))
        {
            Debug.LogWarning($"Car with ID {carData.carId} already exists in database");
            return;
        }

        carDatabase.Add(carData);
    }

    public void AddCarToDatabase(string carId, GameObject carPrefab)
    {
        AddCarToDatabase(new CarData { carId = carId, carPrefab = carPrefab });
    }

    public GameObject GetCarPrefabById(string carId)
    {
        CarData carData = carDatabase.FirstOrDefault(c => c.carId == carId);
        return carData?.carPrefab;
    }

    public CarStats GetCarStatsById(string carId)
    {
        GameObject prefab = GetCarPrefabById(carId);
        if (prefab != null)
        {
            // Get CarStats from the prefab (it's attached to the body)
            return prefab.GetComponentInChildren<CarStats>(true);
        }
        return null;
    }

    public List<string> GetAllCarIds()
    {
        return carDatabase.Select(c => c.carId).ToList();
    }

    public bool CarExists(string carId)
    {
        return carDatabase.Any(c => c.carId == carId);
    }

    public void SetCurrentCar(string carId)
    {
        if (!CarExists(carId))
        {
            Debug.LogError($"Car with ID {carId} not found in database!");
            return;
        }

        currentCarId = carId;
        PlayerPrefs.SetString(GameConstants.CURRENT_CAR, carId);
        PlayerPrefs.Save();

        if (GameManager.Instance != null)
        {
            GameObject carPrefab = GetCarPrefabById(carId);
            if (carPrefab != null)
            {
                GameManager.Instance.SetCurrentCar(carPrefab, true);
            }
        }

        UpdateCarNameDisplay(); 

    }

    public string GetCurrentCarId()
    {
        return currentCarId;
    }

    public GameObject GetCurrentCarPrefab()
    {
        return GetCarPrefabById(currentCarId);
    }

    // KEEP THIS METHOD - It gets CarStats from the current car PREFAB
    public CarStats GetCurrentCarStats()
    {
        return GetCarStatsById(currentCarId);
    }

    private void LoadCurrentCar()
    {
        if (PlayerPrefs.HasKey(GameConstants.CURRENT_CAR))
        {
            string savedCarId = PlayerPrefs.GetString(GameConstants.CURRENT_CAR);
            if (CarExists(savedCarId))
            {
                currentCarId = savedCarId;

                // SET THE CURRENT CAR REFERENCE IN GAME MANAGER
                if (GameManager.Instance != null)
                {
                    // Get the prefab and set it as current car (even though it's not spawned yet)
                    GameObject carPrefab = GetCarPrefabById(currentCarId);
                    if (carPrefab != null)
                    {
                        GameManager.Instance.SetCurrentCar(carPrefab, true);
                    }
                }

            }
            else
            {
                Debug.LogWarning($"Saved car ID {savedCarId} not found in database, using default");
                SetDefaultCar();
            }
        }
        else
        {
            SetDefaultCar();
        }
    }

    private void SetDefaultCar()
    {
        if (carDatabase.Count > 0)
        {
            currentCarId = carDatabase[2].carId;

            if (GameManager.Instance != null)
            {
                GameObject carPrefab = GetCarPrefabById(currentCarId);
                if (carPrefab != null)
                {
                    GameManager.Instance.SetCurrentCar(carPrefab, true);
                }
            }

            Debug.Log($"Using default car: {currentCarId}");
        }
        else
        {
            Debug.LogError("No cars in database!");
        }
    }


    public void UpdateCarNameDisplay()
    {
        if (carNameText != null)
        {
            carNameText.text = GetCurrentCarDisplayName();
        }
    }

    public CarStats GetCurrentCarStatsInstance()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentCar != null)
        {
            return GameManager.Instance.CurrentCar.GetComponentInChildren<CarStats>(true);
        }
        return null;
    }

    public GameObject GetCurrentCarInstance()
    {
        return GameManager.Instance != null ? GameManager.Instance.CurrentCar : null;
    }

    public string GetCarDisplayName(string carId)
    {
        CarStats stats = GetCarStatsById(carId);
        return stats != null ? stats.CarId : carId;
    }

    public string GetCurrentCarDisplayName()
    {
        // First try to get from spawned instance
        CarStats instanceStats = GetCurrentCarStatsInstance();
        if (instanceStats != null)
        {
            return instanceStats.CarId;
        }

        // Fallback to prefab stats
        CarStats prefabStats = GetCurrentCarStats();
        return prefabStats != null ? prefabStats.CarId : currentCarId;
    }

    // Editor utility method to auto-populate from prefabs
#if UNITY_EDITOR
    [ContextMenu("Auto-Populate from Prefabs")]
    private void AutoPopulateFromPrefabs()
    {
        carDatabase.Clear();

        string[] prefabGuids = UnityEditor.AssetDatabase.FindAssets("Player_ t:Prefab", new[] { "Assets/Prefabs/Vehicles/Player" });

        foreach (string guid in prefabGuids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab != null)
            {
                CarStats stats = prefab.GetComponentInChildren<CarStats>(true);
                if (stats != null && !string.IsNullOrEmpty(stats.CarId))
                {
                    AddCarToDatabase(stats.CarId, prefab);
                    Debug.Log($"Added car to database: {stats.CarId}");
                }
                else
                {
                    Debug.LogWarning($"Prefab {prefab.name} has no CarStats component or empty CarId");
                }
            }
        }
    }
#endif
}