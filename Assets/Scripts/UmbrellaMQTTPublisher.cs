using UnityEngine;

public class UmbrellaMQTTPublisher : MonoBehaviour
{
    [Header("Core Engine Link")]
    [SerializeField] private UmbrellaControllerA umbrellaEngine;

    [Header("Configured MQTT Topics")]
    [SerializeField] private string windTopic = "DuoResidence/Amr/SmartShades/Wind";
    [SerializeField] private string rainTopic = "DuoResidence/Amr/SmartShades/Rain";
    [SerializeField] private string solarTopic = "DuoResidence/Amr/SmartShades/Solar";

    [Header("Telemetry Settings")]
    [Tooltip("Frequency of network broadcasts in seconds (e.g., 0.5s = 2Hz telemetry stream).")]
    [SerializeField] private float publishInterval = 0.5f;

    private float _networkTimer = 0f;
    
    // Memory cache to optimize bandwidth by skipping identical consecutive transmissions
    private float _lastPublishedWind = -1f;
    private float _lastPublishedRain = -1f;
    private float _lastPublishedSolar = -1f;

    void Start()
    {
        if (umbrellaEngine == null)
        {
            umbrellaEngine = GetComponent<UmbrellaControllerA>();
        }
    }

    void Update()
    {
        // Safety execution gate: Only run if the core engine is bound and the network manager is online
        if (umbrellaEngine == null || MQTTConnectionManager.Instance == null) return;

        _networkTimer += Time.deltaTime;
        if (_networkTimer >= publishInterval)
        {
            _networkTimer = 0f;
            StreamTelemetryOutbound();
        }
    }

    /// <summary>
    /// Evaluates current scalar parameters and pipes delta shifts directly to your MQTT broker engine.
    /// </summary>
    private void StreamTelemetryOutbound()
    {
        // 1. Stream Wind Speed
        if (!Mathf.Approximately(umbrellaEngine.windSpeed, _lastPublishedWind))
        {
            _lastPublishedWind = umbrellaEngine.windSpeed;
            MQTTConnectionManager.Instance.PublishTopic(windTopic, _lastPublishedWind.ToString("F1"), retain: false);
        }

        // 2. Stream Rain Intensity
        if (!Mathf.Approximately(umbrellaEngine.rainIntensity, _lastPublishedRain))
        {
            _lastPublishedRain = umbrellaEngine.rainIntensity;
            MQTTConnectionManager.Instance.PublishTopic(rainTopic, _lastPublishedRain.ToString("F0"), retain: false);
        }

        // 3. Stream Solar Irradiance
        if (!Mathf.Approximately(umbrellaEngine.solarIrradiance, _lastPublishedSolar))
        {
            _lastPublishedSolar = umbrellaEngine.solarIrradiance;
            MQTTConnectionManager.Instance.PublishTopic(solarTopic, _lastPublishedSolar.ToString("F0"), retain: false);
        }
    }
}