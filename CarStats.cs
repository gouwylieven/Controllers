using UnityEngine;
using TMPro;

public class CarStats : MonoBehaviour
{
    [Header("Car Identification")]
    [SerializeField] private string _carId;
    public string CarId => _carId;

    [Header("UI References")]
    [SerializeField] private TMP_Text _carNameText;

    [Header("Car Settings")]
    public float topSpeedKMH;
    public float horsepower; // in hp
    public float engineSize; //in cc
    public float zeroToSixtyTime; // 0-60 in seconds
    public float maxRpm;
    public float gearShiftTime;
    public bool isManual;
    public float maxFuel;          // in liters
    public float fuelConsumption;
    public float mass;
    public int gears;
    public int passengers;
    public int boothsizeInL;
    public float totalDistanceKM = 0f;
    public string country;
    public string brand;
    public string year;
    public bool isOwned;
    public float price;
    public int carClass;
    public string designer;
    public bool hasInsurance;

    [Header("Performance Tuning")]
    public float accelerationMultiplier = 1f;
    public float odometerMultiplier = 1f;

    private void Awake()
    {
        if (string.IsNullOrEmpty(_carId))
        {
            _carId = gameObject.name;
        }

        // Update the car name text if the reference is set
        if (_carNameText != null)
        {
            _carNameText.text = _carId;
        }

        LoadInsuranceStatus();
        LoadOwnershipStatus(); // Load ownership status on awake
    }

    public void SetCarNameTextReference(TMP_Text nameText)
    {
        _carNameText = nameText;
        UpdateCarNameText();
    }

    // Public method to update the car name text
    public void UpdateCarNameText()
    {
        if (_carNameText != null)
        {
            _carNameText.text = _carId;
        }
    }

    public void SaveInsuranceStatus()
    {
        PlayerPrefs.SetInt($"{GameConstants.CAR_INSURANCE_PREFIX}{_carId}", hasInsurance ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void LoadInsuranceStatus()
    {
        if (string.IsNullOrEmpty(_carId))
        {
            Debug.LogWarning("CarId is null or empty - cannot load insurance status");
            return;
        }

        string key = $"{GameConstants.CAR_INSURANCE_PREFIX}{_carId}";

        if (PlayerPrefs.HasKey(key))
        {
            int savedValue = PlayerPrefs.GetInt(key);
            hasInsurance = savedValue == 1;
        }
        else
        {
            hasInsurance = false;
        }
    }

    // NEW: Save ownership status
    public void SaveOwnershipStatus()
    {
        if (string.IsNullOrEmpty(_carId))
        {
            Debug.LogWarning("CarId is null or empty - cannot save ownership status");
            return;
        }

        PlayerPrefs.SetInt($"{GameConstants.CAR_OWNED_PREFIX}{_carId}", isOwned ? 1 : 0);
        PlayerPrefs.Save();
    }

    // NEW: Load ownership status
    public void LoadOwnershipStatus()
    {
        if (string.IsNullOrEmpty(_carId))
        {
            Debug.LogWarning("CarId is null or empty - cannot load ownership status");
            return;
        }

        string key = $"{GameConstants.CAR_OWNED_PREFIX}{_carId}";

        if (PlayerPrefs.HasKey(key))
        {
            int savedValue = PlayerPrefs.GetInt(key);
            isOwned = savedValue == 1;
        }
        else
        {
            isOwned = false;
        }
    }

    // NEW: Method to set ownership and save it
    public void SetOwned(bool owned)
    {
        isOwned = owned;
        SaveOwnershipStatus();
    }
}