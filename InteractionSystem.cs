using UnityEngine;
using UnityEngine.InputSystem;

public class InteractionSystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerFoodInteraction _foodInteraction;
    [SerializeField] private PlayerCarwashInteraction _carwashInteraction;
    [SerializeField] private PlayerInsuranceInteraction _insuranceInteraction;
    [SerializeField] private PlayerFuelingInteraction _fuelingInteraction;
    [SerializeField] private PlayerCarDealerInteraction _carDealerInteraction;

    private DriveCar _driveCar;
    private CollisionDetector _collisionDetector;
    private HouseData _currentHouseData;

    private void Awake()
    {
        // Get references when the scene starts
        GetCarReferences();

        // Initialize interactions if not set in inspector
        if (_foodInteraction == null)
            _foodInteraction = FindFirstObjectByType<PlayerFoodInteraction>();

        if (_carwashInteraction == null)
            _carwashInteraction = FindFirstObjectByType<PlayerCarwashInteraction>();

        if (_insuranceInteraction == null)
            _insuranceInteraction = FindFirstObjectByType<PlayerInsuranceInteraction>();

        if (_fuelingInteraction == null)
            _fuelingInteraction = FindFirstObjectByType<PlayerFuelingInteraction>();

        if (_carDealerInteraction == null)
            _carDealerInteraction = FindFirstObjectByType<PlayerCarDealerInteraction>();
    }

    private void OnEnable()
    {
        // Subscribe to car change events
        if (GameManager.Instance != null)
            GameManager.Instance.OnCarChanged += OnCarChanged;
    }

    private void OnDisable()
    {
        // Unsubscribe from events
        if (GameManager.Instance != null)
            GameManager.Instance.OnCarChanged -= OnCarChanged;

        if (_collisionDetector != null)
        {
            _collisionDetector.OnHouseEnter -= HandleHouseEnter;
            _collisionDetector.OnHouseExit -= HandleHouseExit;
        }
    }

    private void OnCarChanged()
    {
        // When car changes, update our references
        GetCarReferences();
    }

    private void GetCarReferences()
    {
        // Clear existing references
        if (_collisionDetector != null)
        {
            _collisionDetector.OnHouseEnter -= HandleHouseEnter;
            _collisionDetector.OnHouseExit -= HandleHouseExit;
        }

        // Get new references from current car
        if (GameManager.Instance.CurrentCar != null)
        {
            _driveCar = GameManager.Instance.CurrentCar.GetComponentInChildren<DriveCar>();
            _collisionDetector = GameManager.Instance.CurrentCar.GetComponentInChildren<CollisionDetector>();

            if (_collisionDetector != null)
            {
                _collisionDetector.OnHouseEnter += HandleHouseEnter;
                _collisionDetector.OnHouseExit += HandleHouseExit;
            }
        }
        else
        {
            _driveCar = null;
            _collisionDetector = null;
        }
    }

    private void HandleHouseEnter(HouseData houseData)
    {
        _currentHouseData = houseData;

        // Pass house data to all interaction systems
        if (_carwashInteraction != null)
            _carwashInteraction.HandleHouseEnter(houseData);

        if (_insuranceInteraction != null)
            _insuranceInteraction.HandleHouseEnter(houseData);

        if (_carDealerInteraction != null)
            _carDealerInteraction.HandleHouseEnter(houseData);

    }

    private void HandleHouseExit(HouseData houseData)
    {
        if (_currentHouseData == houseData)
        {
            _currentHouseData = null;

            // Pass house exit to all interaction systems
            if (_carwashInteraction != null)
                _carwashInteraction.HandleHouseExit(houseData);

            if (_insuranceInteraction != null)
                _insuranceInteraction.HandleHouseExit(houseData);

            if (_carDealerInteraction != null)
                _carDealerInteraction.HandleHouseExit(houseData);
        }
    }

    public void OnInteract(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        if (_driveCar != null && _driveCar.GetIsAtStandstill() && _currentHouseData != null)
        {
            // First check for car wash
            if (_currentHouseData.betekenaar &&
                !string.IsNullOrEmpty(_currentHouseData.betekenaarText) &&
                _currentHouseData.betekenaarText.Contains("CARWASH", System.StringComparison.OrdinalIgnoreCase))
            {
                // NEW: Use direct method call like insurance
                _carwashInteraction.OnCarwash();
            }
            // Then check for insurance
            else if (_currentHouseData.betekenaar &&
                    !string.IsNullOrEmpty(_currentHouseData.betekenaarText) &&
                    _currentHouseData.betekenaarText.Contains("INSURANCE", System.StringComparison.OrdinalIgnoreCase))
            {
                PlayerInsuranceInteraction.Instance.OnInsurance();
            }

            else if (_currentHouseData.betekenaar &&
                    !string.IsNullOrEmpty(_currentHouseData.betekenaarText) &&
                    _currentHouseData.betekenaarText.Contains("DEALERSHIP", System.StringComparison.OrdinalIgnoreCase))
            {
                _carDealerInteraction.OnCarDealer();
            }

            else if (_currentHouseData.betekenaar &&
                    !string.IsNullOrEmpty(_currentHouseData.betekenaarText) &&
                    _currentHouseData.betekenaarText.Contains("FUELSTATION", System.StringComparison.OrdinalIgnoreCase))
            {
                _fuelingInteraction.OnFuel(context);
            }
            // Then check for food purchase
            else if (_currentHouseData.Items != null && _currentHouseData.Items.Count > 0)
            {
                _foodInteraction.OnBuy();
            }
        }
    }
}