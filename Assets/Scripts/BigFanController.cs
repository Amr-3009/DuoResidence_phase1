using UnityEngine;

public class BigFanController : MonoBehaviour
{
    [Header("Speed Settings")]
    public float maxRPM = 720f;
    [Range(0f, 100f)] public float operatingPercentage = 50f;

    [Header("Rotation Axis")]
    [SerializeField] private Vector3 rotationAxis = Vector3.up;

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
        
        Debug.Log($"<color=#FF77AA><b>[HVAC Main Extractor {gameObject.name}]:</b></color> High-Volume unit scaling. Now operating at {operatingPercentage}% ({currentRPM} RPM).");
    }
}