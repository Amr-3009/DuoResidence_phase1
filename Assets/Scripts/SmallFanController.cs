using UnityEngine;

public class SmallFanController : MonoBehaviour
{
    [Header("Speed Settings")]
    public float maxRPM = 1440f;
    [Range(0f, 100f)] public float operatingPercentage = 50f;

    [Header("Rotation Axis")]
    [SerializeField] private Vector3 rotationAxis = Vector3.up;

    void Start()
    {
        // Check if manager is already running at startup to sync defaults
        if (AirQualityManager.Instance != null)
        {
            // We wait one frame or safely subscribe
        }
    }

    private void OnEnable()
    {
        AirQualityManager.OnAirQualityRatingChanged += HandleAirQualityChange;
    }

    private void OnDisable()
    {
        AirQualityManager.OnAirQualityRatingChanged -= HandleAirQualityChange;
    }

    private void Update()
    {
        float currentRPM = maxRPM * (operatingPercentage / 100f);
        float degreesPerSecond = (currentRPM / 60f) * 360f;
        transform.Rotate(rotationAxis * degreesPerSecond * Time.deltaTime);
    }

    private void HandleAirQualityChange(float newOperatingPercentage)
    {
        operatingPercentage = newOperatingPercentage;
        float currentRPM = maxRPM * (operatingPercentage / 100f);
        
        // Outputs your exact tracking statement request!
        Debug.Log($"<color=#77CCFF>[HVAC Node {gameObject.name}]:</color> Speed adjusted. Now operating at {operatingPercentage}% ({currentRPM} RPM).");
    }
}