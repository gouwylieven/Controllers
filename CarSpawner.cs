using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

[DefaultExecutionOrder(-85)]
public class CarSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private CinemachineCamera virtualCamera;
    private bool spawnOnAwake = false;

    public GameObject CurrentCar { get; private set; }
    public DriveCar CurrentDriveCar { get; private set; }
    public PlayerInput CurrentPlayerInput { get; private set; }

    private void OnEnable()
    {
        // Subscribe to car changed event
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnCarChanged += HandleCarChanged;
        }
    }

    private void OnDisable()
    {
        // Unsubscribe from car changed event
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnCarChanged -= HandleCarChanged;
        }
    }



    void Start()
    {
        if (spawnOnAwake)
        {
            SpawnCar();
        }
    }

    private void HandleCarChanged()
    {
        SpawnCar();
    }

    public GameObject SpawnCar(GameObject customPrefab = null)
    {
        if (CurrentCar != null)
        {
            DestroyCurrentCar();
        }

        GameObject prefabToSpawn;

        if (customPrefab != null)
        {
            prefabToSpawn = customPrefab;
        }
        else if (CarManager.Instance != null)
        {
            prefabToSpawn = CarManager.Instance.GetCurrentCarPrefab();

            if (prefabToSpawn == null)
            {
                Debug.LogWarning("Current car prefab is null, trying to get default from database");
                if (CarManager.Instance.GetAllCarIds().Count > 0)
                {
                    string firstCarId = CarManager.Instance.GetAllCarIds()[0];
                    prefabToSpawn = CarManager.Instance.GetCarPrefabById(firstCarId);
                }
            }
        }
        else
        {
            Debug.LogError("CarManager instance not found and no custom prefab provided!");
            return null;
        }

        if (prefabToSpawn == null)
        {
            Debug.LogError("No car prefab to spawn!");
            return null;
        }

        // Check for saved position
        bool hasSavedPosition = PlayerPrefs.HasKey(GameConstants.CAR_POSITION_X);

        CurrentCar = Instantiate(
            prefabToSpawn,
            hasSavedPosition ? Vector3.zero : spawnPoint.position,
            hasSavedPosition ? Quaternion.identity : spawnPoint.rotation
        );

        CurrentDriveCar = CurrentCar.GetComponentInChildren<DriveCar>(true);

        if (CurrentDriveCar == null)
        {
            Debug.LogError("Spawned car doesn't have a DriveCar component!");
            Destroy(CurrentCar);
            CurrentCar = null;
            return null;
        }

        CurrentDriveCar.InitializeCarComponents();

        if (!InitializeInputSystem())
        {
            Debug.LogError("Failed to initialize input system for the car!");
            Destroy(CurrentCar);
            CurrentCar = null;
            CurrentDriveCar = null;
            return null;
        }

        SetupCamera();
        RegisterWithGameManager();

        return CurrentCar;
    }

    private bool InitializeInputSystem()
    {
        CurrentPlayerInput = CurrentDriveCar.GetComponent<PlayerInput>();
        if (CurrentPlayerInput == null || CurrentPlayerInput.actions == null)
        {
            Debug.LogError("Car doesn't have PlayerInput component or actions!");
            return false;
        }

        InputAction moveAction = CurrentPlayerInput.actions.FindAction("Move", throwIfNotFound: false);
        if (moveAction == null)
        {
            Debug.LogError("Move action not found in input actions!");
            return false;
        }

        CurrentPlayerInput.enabled = true;
        CurrentPlayerInput.ActivateInput();
        return true;
    }

    private void SetupCamera()
    {
        if (virtualCamera != null)
        {
            virtualCamera.Follow = CurrentDriveCar.transform;
            virtualCamera.LookAt = CurrentDriveCar.transform;
        }
    }

    private void RegisterWithGameManager()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetCurrentCar(CurrentDriveCar.gameObject, true);

            // Notify CarManager to update the display
            if (CarManager.Instance != null)
            {
                CarManager.Instance.UpdateCarNameDisplay();
            }
        }
    }

    public void DestroyCurrentCar()
    {
        if (CurrentCar != null)
        {
            Destroy(CurrentCar);
            CurrentCar = null;
            CurrentDriveCar = null;
            CurrentPlayerInput = null;
        }
    }
}