using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class DriveCar : MonoBehaviour
{
    [Header("Car Physics")]
    [SerializeField] private Rigidbody2D _frontTireRB;
    [SerializeField] private Rigidbody2D _backTireRB;
    [SerializeField] private float _speed = 150f;
    [SerializeField] private float _brakeRate = 15f;

    [Header("Collision Detection")]
    [SerializeField] private CollisionDetector _collisionDetector;
    [SerializeField] private float _flipDuration = 0.15f;

    [Header("Visual References")]
    [SerializeField] private SpriteRenderer _bodySpriteRenderer;
    [SerializeField] private Transform _bodyTransform;

    [Header("Horn Settings")]
    [SerializeField] private AudioSource _hornAudioSource;
    [SerializeField] private float _hornCooldown = 0.5f;

    [Header("Gear Display")]
    [SerializeField] private TextMeshProUGUI _gearText;
    private string _gearTextName = "GearText";

    private bool _canHorn = true;

    private bool _isFlipping = false;
    private float _moveInput;
    private bool _isCruiseControlActive = false;
    private float _cruiseControlSpeed;
    private bool _isInputActive = false;
    private bool _isBraking = false;
    private bool _isAtStandstill = true;
    private float _currentVelocity = 0f;
    private float _lastMovementDirection = 0f;
    private int _currentFacingDirection = -1;
    private CarStats _carStats;
    private int _currentGear = 0;
    private float[] _gearSpeedLimits;
    private float _currentRpm = 0f;
    private float _lastShiftTime = 0f;
    private Speedometer _speedometer;
    public bool _isShifting = false;
    private float _shiftStartTime = 0f;
    private float[] _gearTorqueMultipliers;
    private float _previousRpm = 0f;
    private bool _wasBrakingBeforeShift = false;
    private bool _isBrakeInputActive = false;



    // Target gear system
    private int _targetGear = 0;

    public Rigidbody2D GetFrontTireRB() => _frontTireRB;
    public Rigidbody2D GetBackTireRB() => _backTireRB;
    public int GetCurrentFacingDirection() => _currentFacingDirection;
    public bool GetIsAtStandstill() => _isAtStandstill;

    public float CurrentSpeedKMH
    {
        get
        {
            float avgAngularVelocity = (_frontTireRB.angularVelocity + _backTireRB.angularVelocity) / 2f;
            float wheelRadius = 0.25f;
            float speedMetersPerSecond = Mathf.Abs(avgAngularVelocity) * wheelRadius;
            return speedMetersPerSecond * 3.6f;
        }
    }

    public event System.Action<float> OnRPMChanged;

    // Property to handle gear changes with event
    private int CurrentGear
    {
        get => _currentGear;
        set
        {
            if (_currentGear != value)
            {
                _currentGear = value;
                OnGearChanged();
            }
        }
    }

    // Property to handle shift state changes with event
    public bool IsShifting
    {
        get => _isShifting;
        set
        {
            if (_isShifting != value)
            {
                _isShifting = value;
                OnShiftStateChanged();
            }
        }
    }

    private void Awake()
    {
        _currentFacingDirection = -1;
        _isAtStandstill = true;
        _lastMovementDirection = 0f;
        _targetGear = 0;

        // Try to find collision detector if not assigned
        if (_collisionDetector == null)
        {
            _collisionDetector = GetComponentInChildren<CollisionDetector>(true);
            if (_collisionDetector == null)
            {
                Debug.LogWarning("CollisionDetector not found in children. Please assign manually.", this);
            }
        }

        // Try to find wheel rigidbodies if not assigned
        if (_frontTireRB == null || _backTireRB == null)
        {
            // Look for wheels in children (they should be direct children of this object)
            Rigidbody2D[] childRBs = GetComponentsInChildren<Rigidbody2D>();
            foreach (Rigidbody2D rb in childRBs)
            {
                if (rb.gameObject != gameObject) // Exclude self
                {
                    if (_frontTireRB == null)
                        _frontTireRB = rb;
                    else if (_backTireRB == null)
                        _backTireRB = rb;
                }
            }

            if (_frontTireRB == null || _backTireRB == null)
            {
                Debug.LogError("Wheel references not set in DriveCar!", this);
            }
        }

        // Since this script is on the "body" object, we need to look for visuals in parent
        if (_bodySpriteRenderer == null)
        {
            _bodySpriteRenderer = GetComponent<SpriteRenderer>();
            if (_bodySpriteRenderer == null)
            {
                Debug.LogWarning("Could not find SpriteRenderer on this object. Please assign _bodySpriteRenderer manually.", this);
            }
        }

        if (_bodyTransform == null)
        {
            _bodyTransform = transform;
        }

        // Try to find gear text dynamically
        FindGearText();
    }

    private void FindGearText()
    {
        // If already assigned, no need to search
        if (_gearText != null) return;

        // Search for the gear text in the scene
        GameObject gearTextObj = GameObject.Find(_gearTextName);
        if (gearTextObj != null)
        {
            _gearText = gearTextObj.GetComponent<TextMeshProUGUI>();
        }
    }

    public void InitializeCarComponents()
    {
        if (_frontTireRB == null || _backTireRB == null)
        {
            Rigidbody2D[] wheels = GetComponentsInChildren<Rigidbody2D>();
            if (wheels.Length >= 2)
            {
                _frontTireRB = wheels[0];
                _backTireRB = wheels[1];
            }
            else
            {
                Debug.LogError("DriveCar: Could not find wheel Rigidbody2Ds!", this);
            }
        }

        _carStats = GetComponent<CarStats>();
        if (_carStats != null)
        {
            SetupGearLimits();
        }

        // Cache speedometer reference for gear logic only
        _speedometer = FindFirstObjectByType<Speedometer>();

        // Initialize gear text - call this method to ensure it's found
        InitializeGearText();

        UpdateGearDisplay();
    }

    private void InitializeGearText()
    {
        // If already assigned, no need to search
        if (_gearText != null) return;

        // Search for the gear text in the scene
        GameObject gearTextObj = GameObject.Find(_gearTextName);
        if (gearTextObj != null)
        {
            _gearText = gearTextObj.GetComponent<TextMeshProUGUI>();
        }
    }

    private void SetupGearLimits()
    {
        if (_carStats == null || _carStats.gears <= 0) return;

        _gearSpeedLimits = new float[_carStats.gears + 1];
        _gearTorqueMultipliers = new float[_carStats.gears + 1];

        float topSpeed = _carStats.topSpeedKMH;
        int g = _carStats.gears;

        // Pre-calculate torque multipliers for each gear
        float maxMultiplier = 2.2f;
        float minMultiplier = 1f;

        for (int i = 1; i <= g; i++)
        {
            float ratio = Mathf.Pow((float)i / g, 1.1f);
            _gearSpeedLimits[i] = topSpeed * ratio;

            // Pre-calculate torque multiplier for this gear
            float gearRatio = (float)(i - 1) / (g - 1);
            _gearTorqueMultipliers[i] = Mathf.Lerp(maxMultiplier, minMultiplier, Mathf.Pow(gearRatio, 0.7f));
        }

        // Set neutral gear multiplier
        _gearTorqueMultipliers[0] = 1f;
    }

    // Simplified GetGearTorqueMultiplier method
    private float GetGearTorqueMultiplier()
    {
        if (_gearTorqueMultipliers == null || CurrentGear < 0 || CurrentGear >= _gearTorqueMultipliers.Length)
            return 1f;

        return _gearTorqueMultipliers[CurrentGear];
    }

    private void UpdateRPM(float speed, float gearLimit)
    {
        float newRpm = Mathf.Clamp((speed / gearLimit) * _carStats.maxRpm, 800f, _carStats.maxRpm);

        // Only trigger event if RPM changed significantly (reduces unnecessary updates)
        if (Mathf.Abs(newRpm - _previousRpm) > 10f)
        {
            _currentRpm = newRpm;
            _previousRpm = _currentRpm;
            OnRPMChanged?.Invoke(_currentRpm);
        }
        else
        {
            _currentRpm = newRpm;
        }
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        if (context.canceled)
        {
            _isInputActive = false;
            _moveInput = 0f;
            _isBrakeInputActive = false; // Reset brake input when released

            // If we were braking, stop braking and resume cruise control
            if (_isBraking)
            {
                _isBraking = false;

                if (!_isAtStandstill)
                {
                    _isCruiseControlActive = true;
                    _cruiseControlSpeed = (_frontTireRB.angularVelocity + _backTireRB.angularVelocity) / 2f;
                }
            }
            else if (!_isAtStandstill)
            {
                _isCruiseControlActive = true;
                _cruiseControlSpeed = (_frontTireRB.angularVelocity + _backTireRB.angularVelocity) / 2f;
            }
            else if (_isAtStandstill)
            {
                _isBraking = false;
                _isCruiseControlActive = false;
                _lastMovementDirection = 0f;
            }
        }
        else if (context.performed)
        {
            HandleMovementInput(context);
        }
    }

    private void HandleMovementInput(InputAction.CallbackContext context)
    {
        float currentInput = context.ReadValue<float>();
        _isCruiseControlActive = false;
        _isInputActive = true;
        _moveInput = currentInput;

        float avgVelocity = (_frontTireRB.angularVelocity + _backTireRB.angularVelocity) / 1.9f;

        if (_isAtStandstill && currentInput > 0 && _currentFacingDirection == -1)
        {
            FlipCar();
            _isAtStandstill = false;
            _isBraking = false;
            _lastMovementDirection = currentInput;
            return;
        }

        if (_isAtStandstill)
        {
            HandleStandstillMovement(currentInput, avgVelocity);
            return;
        }

        // Update brake input state
        _isBrakeInputActive = ShouldBrake(currentInput, avgVelocity);

        if (_isBrakeInputActive)
        {
            _isBraking = true;
            _currentVelocity = avgVelocity;
        }
        else
        {
            _isBraking = false;
            _lastMovementDirection = currentInput;
        }
    }


    private void HandleStandstillMovement(float currentInput, float avgVelocity)
    {
        if ((currentInput > 0 && _currentFacingDirection == -1) ||
            (currentInput < 0 && _currentFacingDirection == 1))
        {
            FlipCar();
        }

        _isAtStandstill = false;
        _isBraking = false;
        _lastMovementDirection = currentInput;
    }

    private bool ShouldBrake(float currentInput, float avgVelocity)
    {
        if (Mathf.Abs(avgVelocity) > 0.1f)
        {
            return (currentInput > 0 && avgVelocity > 0.1f) ||
                   (currentInput < 0 && avgVelocity < -0.1f);
        }

        if (Mathf.Abs(_lastMovementDirection) > 0.1f)
        {
            return (_lastMovementDirection > 0 && currentInput < 0) ||
                   (_lastMovementDirection < 0 && currentInput > 0);
        }

        return false;
    }

    public void FlipCarWithWheels()
    {
        float frontWheelSpeed = _frontTireRB.angularVelocity;
        float backWheelSpeed = _backTireRB.angularVelocity;

        int oldFacing = _currentFacingDirection;
        FlipCar();
        int flipFactor = oldFacing * _currentFacingDirection;

        _frontTireRB.angularVelocity = frontWheelSpeed * flipFactor;
        _backTireRB.angularVelocity = backWheelSpeed * flipFactor;
    }

    public void ResetStateAfterTeleport(float angularVelocity)
    {
        _frontTireRB.angularVelocity = angularVelocity;
        _backTireRB.angularVelocity = angularVelocity;
        _isAtStandstill = false;
        _isBraking = false;
        _isCruiseControlActive = false;
        _isInputActive = false;
        _moveInput = 0f;
    }

    protected void FlipCar()
    {
        if (_isFlipping) return;
        StartCoroutine(VisualFlipSequence());
    }

    private IEnumerator VisualFlipSequence()
    {
        _isFlipping = true;
        Rigidbody2D carRB = GetComponent<Rigidbody2D>();
        RigidbodyType2D previousBodyType = carRB.bodyType;
        carRB.bodyType = RigidbodyType2D.Kinematic;

        float frontWheelSpeed = _frontTireRB.angularVelocity;
        float backWheelSpeed = _backTireRB.angularVelocity;

        yield return new WaitForSeconds(_flipDuration * 0.5f);

        _currentFacingDirection *= -1;

        if (_bodyTransform != null)
        {
            Vector3 scale = _bodyTransform.localScale;
            scale.x *= -1;
            _bodyTransform.localScale = scale;
        }

        yield return new WaitForSeconds(_flipDuration * 0.5f);

        carRB.bodyType = previousBodyType;
        _frontTireRB.angularVelocity = -frontWheelSpeed;
        _backTireRB.angularVelocity = -backWheelSpeed;

        _isFlipping = false;
    }

    private void FixedUpdate()
    {
        // 1. BRAKING - Only if brake input is active
        if (_isBraking && _isBrakeInputActive)
        {
            HandleBraking();
            return;
        }
        else if (_isBraking && !_isBrakeInputActive)
        {
            // Brake input was released, stop braking
            _isBraking = false;

            // Resume cruise control at current speed
            if (!_isAtStandstill)
            {
                _isCruiseControlActive = true;
                _cruiseControlSpeed = (_frontTireRB.angularVelocity + _backTireRB.angularVelocity) / 2f;
            }
        }

        // 2. GEAR SHIFTING - Second priority
        if (IsShifting)
        {
            HandleShiftProgress();
            return;
        }

        // 3. NORMAL OPERATION - Lowest priority
        HandleNormalDriving();
    }

    private void HandleShiftProgress()
    {
        float shiftElapsed = Time.time - _shiftStartTime;

        if (shiftElapsed >= _carStats.gearShiftTime)
        {
            IsShifting = false;
            CurrentGear = _targetGear;

            // After shift completes, check if we should resume braking
            if (_wasBrakingBeforeShift && _isBrakeInputActive) // Only resume if brake input is still active
            {
                _isBraking = true;
                _wasBrakingBeforeShift = false;
            }
            return;
        }

        // If we were braking before the shift AND brake input is still active, continue braking
        if (_wasBrakingBeforeShift && _isBrakeInputActive)
        {
            _currentVelocity = Mathf.MoveTowards(_currentVelocity, 0f, _brakeRate * Time.fixedDeltaTime);
            _frontTireRB.angularVelocity = _currentVelocity;
            _backTireRB.angularVelocity = _currentVelocity;
        }
        else
        {
            // Apply normal drag during shifting
            float dragFactor = 0.98f;
            _frontTireRB.angularVelocity *= dragFactor;
            _backTireRB.angularVelocity *= dragFactor;
        }
    }

    private void HandleNormalDriving()
    {
        HandleGears();

        if (_isAtStandstill)
        {
            _frontTireRB.angularVelocity = 0f;
            _backTireRB.angularVelocity = 0f;
            return;
        }

        // Prevent movement in neutral gear when in manual mode
        if (_carStats != null && _carStats.isManual && CurrentGear == 0)
        {
            float dragFactor = 0.995f;
            _frontTireRB.angularVelocity *= dragFactor;
            _backTireRB.angularVelocity *= dragFactor;
            return;
        }

        // Apply cruise control
        if (_isCruiseControlActive)
        {
            _frontTireRB.angularVelocity = _cruiseControlSpeed;
            _backTireRB.angularVelocity = _cruiseControlSpeed;
            return;
        }

        // Re-enable cruise control if no input is active
        if (!_isInputActive && !_isAtStandstill)
        {
            _isCruiseControlActive = true;
            _cruiseControlSpeed = (_frontTireRB.angularVelocity + _backTireRB.angularVelocity) / 2f;
        }

        // Apply torque
        if (_isInputActive)
        {
            float torque = -_moveInput * _speed * Time.fixedDeltaTime;
            float torqueMultiplier = GetGearTorqueMultiplier();
            torque *= torqueMultiplier;

            if (_carStats != null && _gearSpeedLimits != null && _currentGear > 0)
            {
                float currentSpeed = _speedometer != null ? _speedometer.CurrentSpeedKMH : CurrentSpeedKMH;
                float gearLimit = _gearSpeedLimits[_currentGear];

                float limitFactor = 1f;
                if (currentSpeed >= gearLimit)
                {
                    limitFactor = 0f;
                }
                else if (currentSpeed > gearLimit * 0.9f)
                {
                    limitFactor = Mathf.Clamp01((gearLimit - currentSpeed) / (gearLimit * 0.1f));
                }

                _frontTireRB.AddTorque(torque * limitFactor);
                _backTireRB.AddTorque(torque * limitFactor);
            }
            else
            {
                _frontTireRB.AddTorque(torque);
                _backTireRB.AddTorque(torque);
            }
        }
    }

    private void HandleGears()
    {
        if (_carStats == null || _gearSpeedLimits == null || IsShifting) return;

        // Skip automatic gear handling if manual transmission is enabled
        if (_carStats.isManual)
        {
            HandleManualGears();
            return;
        }

        // Original automatic gear logic
        float speed = _speedometer != null ? _speedometer.CurrentSpeedKMH : CurrentSpeedKMH;

        // Standstill check
        if (speed < 2f && !_isInputActive)
        {
            if (CurrentGear != 0)
                CurrentGear = 0;
            _currentRpm = 0f;
            return;
        }

        if (CurrentGear == 0 && _isInputActive)
        {
            CurrentGear = 1;
            return;
        }

        // Define shift thresholds
        float upshiftThreshold = _gearSpeedLimits[CurrentGear] * 0.96f;
        float downshiftThreshold = CurrentGear > 1 ? _gearSpeedLimits[CurrentGear - 1] * 0.7f : 0f;

        // Check for upshift
        if (CurrentGear < _carStats.gears && speed > upshiftThreshold)
        {
            StartGearShift(CurrentGear + 1);
        }
        // Check for downshift
        else if (CurrentGear > 1 && speed < downshiftThreshold)
        {
            StartGearShift(CurrentGear - 1);
        }

        // Update RPM based on current gear (even during shift)
        float gearLimit = _gearSpeedLimits[Mathf.Max(1, CurrentGear)];
        UpdateRPM(speed, gearLimit);
    }

    private void HandleManualGears()
    {
        float speed = _speedometer != null ? _speedometer.CurrentSpeedKMH : CurrentSpeedKMH;

        // In manual mode, NEVER automatically change gears based on speed or input
        // The player has full control over gear selection

        // Update RPM based on current gear
        if (CurrentGear > 0)
        {
            float gearLimit = _gearSpeedLimits[CurrentGear];
            UpdateRPM(speed, gearLimit);

            // Optional: Add engine stalling if RPM gets too low in higher gears
            if (_currentRpm < 500f && CurrentGear > 1)
            {
                // Engine stall - could stop the car or force downshift
                // For now, just limit torque application
            }
        }
        else
        {
            _currentRpm = 800f; // Idle RPM when in neutral
        }
    }

    private void OnGearChanged()
    {
        UpdateGearDisplay();
    }

    private void OnShiftStateChanged()
    {
        UpdateGearDisplay();
    }

    private void UpdateGearDisplay()
    {
        if (_gearText == null) return;

        if (IsShifting)
        {
            // Show "N" during any shifting process
            _gearText.text = "N";
        }
        else if (CurrentGear == 0)
        {
            _gearText.text = "N";
        }
        else
        {
            _gearText.text = CurrentGear.ToString();
        }
    }

    private void StartGearShift(int newGear)
    {
        // Remember if we were braking before starting the shift
        _wasBrakingBeforeShift = _isBraking;

        IsShifting = true;
        _shiftStartTime = Time.time;
        _targetGear = newGear;
        CurrentGear = newGear;

        // Disable cruise control when starting a gear shift
        _isCruiseControlActive = false;

        // Temporarily disable braking flag during shift (but preserve the intent)
        _isBraking = false;
    }

    // Update target gear during shifting (restarts shift timer)
    private void UpdateGearTarget(int targetGear)
    {
        _targetGear = targetGear;

        if (IsShifting)
        {
            // Restart the shift timer for the new target
            _shiftStartTime = Time.time;
        }
        else
        {
            // Start a new shift
            StartGearShift(targetGear);
        }
    }

    // Queue a gear shift request
    private void QueueGearShift(int targetGear)
    {
        // Update the target gear
        _targetGear = targetGear;

        // If we're not currently shifting, start immediately
        if (!IsShifting)
        {
            StartGearShift(targetGear);
        }
    }

    public void OnGearUp(InputAction.CallbackContext context)
    {
        // ONLY respond to the performed phase, ignore canceled/started phases
        if (!context.performed) return;

        if (_carStats != null && _carStats.isManual)
        {
            // Only allow gear up if not at maximum gear
            if (_targetGear < _carStats.gears)
            {
                int newTargetGear = _targetGear + 1;

                // Immediate shift from neutral (0) to first gear (1)
                if (_targetGear == 0)
                {
                    _targetGear = 1;
                    CurrentGear = 1; // Direct assignment, no shift time
                }
                else
                {
                    UpdateGearTarget(newTargetGear);
                }
            }
        }
    }

    public void OnGearDown(InputAction.CallbackContext context)
    {
        // ONLY respond to the performed phase, ignore canceled/started phases
        if (!context.performed) return;

        if (_carStats != null && _carStats.isManual)
        {
            // Allow gear down if not already at minimum (gear 0 = neutral)
            if (_targetGear > 0)
            {
                int newTargetGear = _targetGear - 1;

                // Immediate shift from first gear (1) to neutral (0)
                if (_targetGear == 1)
                {
                    _targetGear = 0;
                    CurrentGear = 0; // Direct assignment to neutral, no shift time
                }
                else
                {
                    UpdateGearTarget(newTargetGear);
                }
            }
        }
    }

    public void ConfigureAcceleration(float zeroToSixtyTime, float massKg, float speedCorrectionFactor, float accelerationMultiplier)
    {
        float targetAngularSpeed = 60f / speedCorrectionFactor;
        float angularAcceleration = targetAngularSpeed / zeroToSixtyTime;
        float torquePhysicalConstant = 0.0005f;
        float baseTorque = angularAcceleration * massKg * torquePhysicalConstant;
        _speed = baseTorque * accelerationMultiplier;
    }

    private void HandleBraking()
    {
        // Only brake if brake input is still active
        if (!_isBrakeInputActive)
        {
            _isBraking = false;
            return;
        }

        _currentVelocity = Mathf.MoveTowards(_currentVelocity, 0f, _brakeRate);

        // Check if we've reached standstill based on ACTUAL wheel speeds, not cached velocity
        float avgWheelSpeed = (_frontTireRB.angularVelocity + _backTireRB.angularVelocity) / 2f;

        if (Mathf.Abs(_currentVelocity) < 1f && Mathf.Abs(avgWheelSpeed) < 1f)
        {
            _currentVelocity = 0f;
            _frontTireRB.angularVelocity = 0f;
            _backTireRB.angularVelocity = 0f;
            _isAtStandstill = true;
            _isBraking = false;
            _isBrakeInputActive = false;
        }
        else
        {
            _frontTireRB.angularVelocity = _currentVelocity;
            _backTireRB.angularVelocity = _currentVelocity;
        }
    }

    public void OnHorn(InputAction.CallbackContext context)
    {
        if (context.performed && _canHorn)
        {
            StartCoroutine(HornCooldown());
        }
    }

    private IEnumerator HornCooldown()
    {
        _canHorn = false;
        yield return new WaitForSeconds(_hornCooldown);
        _canHorn = true;
    }

    public void ResetCruiseControl()
    {
        _isCruiseControlActive = false;
        _cruiseControlSpeed = 0f;
        _isInputActive = false;
        _moveInput = 0f;

        // Optional: Add this if you want to prevent any residual torque
        _frontTireRB.angularVelocity = 0f;
        _backTireRB.angularVelocity = 0f;
    }
}