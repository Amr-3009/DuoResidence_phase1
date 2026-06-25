using UnityEngine;

[RequireComponent(typeof(Animator))]
public class UmbrellaControllerA : MonoBehaviour
{
    // Public getter hooks so external dashboard displays can pull values safely
    public float GetWindLimit() => windSpeedLimit;
    public float GetRainLimit() => rainIntensityLimit;
    public float GetSolarLimit() => solarOpeningThreshold;
    [Header("Real-Time Sensor Inputs")]
    [Tooltip("Anemometer reading in meters per second (m/s).")]
    public float windSpeed = 2.5f;

    [Tooltip("Optical sensor reading for rain intensity percentage (0% to 100%).")]
    [Range(0f, 100f)] public float rainIntensity = 0f;

    [Tooltip("Irradiance sensor reading for solar intensity in Watts per square meter (W/m²).")]
    public float solarIrradiance = 550f;

    [Header("Operational Threshold Rules")]
    [SerializeField] private float windSpeedLimit = 12.0f;    // Close immediately if wind >= 12 m/s
    [SerializeField] private float rainIntensityLimit = 15.0f; // OPEN immediately if rain >= 15%
    [SerializeField] private float solarOpeningThreshold = 300.0f; // Open if sun >= 300 W/m²

    private Animator _animator;
    private bool _currentCanopyState = false;

    void Start()
    {
        _animator = GetComponent<Animator>();
        ProcessSensorLogic(forceUpdate: true);
    }

    void Update()
    {
        ProcessSensorLogic(forceUpdate: false);
    }

    /// <summary>
    /// Executes a hierarchical priority check over raw sensor telemetry streams
    /// to safely command the automated canopy deployment system for shade or rain shelter.
    /// </summary>
    private void ProcessSensorLogic(bool forceUpdate)
    {
        bool shouldBeOpen = false;
        string logReason = "";

        // --- HIERARCHICAL PRIORITY EVALUATION (HYDROPHOBIC PARADIGM) ---

        if (windSpeed >= windSpeedLimit)
        {
            // PRIORITY 1: CRITICAL OVERRIDE - High wind always wins. Force close to protect structural integrity.
            shouldBeOpen = false;
            logReason = $"CRITICAL WIND OVERRIDE: Wind speed ({windSpeed} m/s) is dangerous! Forcing mechanical retraction.";
        }
        else if (rainIntensity >= rainIntensityLimit)
        {
            // PRIORITY 2: HYDROPHOBIC PROTECTION - It is raining and winds are safe. Open the canopy to act as an umbrella.
            // This naturally skips the solar check since we want rain shelter regardless of cloud darkness.
            shouldBeOpen = true;
            logReason = $"RAIN PROTECTION MODE: Precipitation detected ({rainIntensity}%). Deploying hydrophobic shelter canvas.";
        }
        else
        {
            // PRIORITY 3: COMFORT BASE CASE - Dry, safe weather. Open only if sun coverage is required.
            if (solarIrradiance >= solarOpeningThreshold)
            {
                shouldBeOpen = true;
                logReason = $"SUNSHADE MODE: Clear weather. Solar intensity ({solarIrradiance} W/m²) requires shade protection.";
            }
            else
            {
                shouldBeOpen = false;
                logReason = $"IDLE RETRACTION: Clear, overcast, or nighttime conditions ({solarIrradiance} W/m²). Canopy secured.";
            }
        }

        // --- ANIMATION STATE MANAGER ENGINE ---

        if (shouldBeOpen != _currentCanopyState || forceUpdate)
        {
            _currentCanopyState = shouldBeOpen;
            _animator.SetBool("IsOpen", shouldBeOpen);

            string colorHeader = shouldBeOpen ? "#66FF66" : "#FF5555";
            Debug.Log($"<color={colorHeader}><b>[Umbrella Engine A]:</b></color> {logReason}");
        }
    }
}