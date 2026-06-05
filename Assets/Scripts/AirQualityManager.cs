using UnityEngine;
using UnityEngine.UI;
using System;

public class AirQualityManager : MonoBehaviour
{
    public static AirQualityManager Instance { get; private set; } 

    public static event Action<float> OnAirQualityRatingChanged; 

    [Header("UI Slider References (Canvas UI)")] 
    [SerializeField] private Slider co2CanvasSlider; 
    [SerializeField] private Slider noCanvasSlider; 

    [Header("Inspector Sliders (Direct Simulation)")] 
    [Range(400f, 5000f)] [SerializeField] private float currentCO2 = 400f; 
    [Range(0f, 100f)] [SerializeField] private float currentNO = 0f; 

    [Header("Environmental Clearing Speeds")] 
    [SerializeField] private float co2MaxDecayRate = 25f; 
    [SerializeField] private float noMaxDecayRate = 0.8f; 

    [Header("Network Throttling Settings")]
    [SerializeField] private float networkPublishInterval = 0.1f;

    [Header("HVAC Reference Specs for Telemetry")] 
    [SerializeField] private float smallFanMaxRPM = 1440f; 
    [SerializeField] private float bigFanMaxRPM = 720f; 

    private string _overrideTopic = "DuoResidence/Amr/Garage/HVAC/Override"; 
    private float _lastCO2; 
    private float _lastNO; 
    private float _networkTimer = 0f;
    private bool _isUpdatingFromCanvas = false; 
    private float _lastBroadcastedRating = -1f; 

    private bool _isTwinOverrideActive = false; 
    private float _overrideCountdownTimer = 0f; 
    private float _overrideTargetPercentage = 20f; 

    [System.Serializable]
    public class HVACTelemetryPayload 
    {
        public float co2; 
        public float no; 
        public float operatingPercentage; 
        public float smallFanRPM; 
        public float bigFanRPM; 
    }

    private void Awake() 
    {
        if (Instance == null) Instance = this; 
        else Destroy(gameObject); 
    }

    void Start() 
    {
        _lastCO2 = currentCO2; 
        _lastNO = currentNO; 

        if (co2CanvasSlider != null) 
        {
            co2CanvasSlider.minValue = 400f; 
            co2CanvasSlider.maxValue = 5000f; 
            co2CanvasSlider.value = currentCO2; 
            co2CanvasSlider.onValueChanged.AddListener(OnCO2CanvasSliderChanged); 
        }

        if (noCanvasSlider != null) 
        {
            noCanvasSlider.minValue = 0f; 
            noCanvasSlider.maxValue = 100f; 
            noCanvasSlider.value = currentNO; 
            noCanvasSlider.onValueChanged.AddListener(OnNOCanvasSliderChanged); 
        }

        MQTTConnectionManager.OnTelemetryMessageReceived += HandleIncomingTwinOverrides; 
        Invoke(nameof(SubscribeToTwinCommandChannel), 0.5f); 

        ForceNetworkBroadcast();
    }

    void Update() 
    {
        // 1. Tick down manual override countdowns
        if (_isTwinOverrideActive) 
        {
            _overrideCountdownTimer -= Time.deltaTime; 
            if (_overrideCountdownTimer <= 0f) 
            {
                _isTwinOverrideActive = false; 
                Debug.Log("<color=green><b>[HVAC System]:</b></color> Twin manual override window expired. Reverting control back to sensors."); 
            }
        }

        // 2. Continuous local high-speed gas decay simulation calculations (RESTORED!)
        float currentRunningPercentage = GetCurrentOperatingPercentage(); 
        float co2Reduction = co2MaxDecayRate * (currentRunningPercentage / 100f) * Time.deltaTime; 
        float noReduction = noMaxDecayRate * (currentRunningPercentage / 100f) * Time.deltaTime; 

        if (currentCO2 > 400f) currentCO2 = Mathf.Max(400f, currentCO2 - co2Reduction); 
        if (currentNO > 0f) currentNO = Mathf.Max(0f, currentNO - noReduction); 

        // 3. Keep standard local UI sliders perfectly responsive
        if (!Mathf.Approximately(currentCO2, _lastCO2)) 
        {
            _lastCO2 = currentCO2; 
            if (co2CanvasSlider != null && !_isUpdatingFromCanvas) co2CanvasSlider.value = currentCO2; 
        }
        if (!Mathf.Approximately(currentNO, _lastNO)) 
        {
            _lastNO = currentNO; 
            if (noCanvasSlider != null && !_isUpdatingFromCanvas) noCanvasSlider.value = currentNO; 
        }

        // 4. Throttled network tick window logic loop
        _networkTimer += Time.deltaTime;
        if (_networkTimer >= networkPublishInterval)
        {
            ForceNetworkBroadcast();
            _networkTimer = 0f;
        }
    }

    // 🚀 SECRET MENU INTEGRATION INTERFACES
    public float GetCO2() => currentCO2;
    public float GetNO() => currentNO;
    
    public void InjectCO2FromMenu(float value) { currentCO2 = value; _lastCO2 = value; }
    public void InjectNOFromMenu(float value) { currentNO = value; _lastNO = value; }

    private void ForceNetworkBroadcast()
    {
        float currentPct = GetCurrentOperatingPercentage(); 
        
        if (!Mathf.Approximately(currentPct, _lastBroadcastedRating)) 
        {
            _lastBroadcastedRating = currentPct; 
            OnAirQualityRatingChanged?.Invoke(currentPct); 
        }

        PublishJsonTelemetry(currentPct); 
    }

    private float GetCurrentOperatingPercentage() 
    {
        if (_isTwinOverrideActive) return _overrideTargetPercentage; 

        int co2Severity = GetCO2Severity(currentCO2); 
        int noSeverity = GetNOSeverity(currentNO); 
        int overallSeverity = Mathf.Max(co2Severity, noSeverity); 

        switch (overallSeverity) 
        {
            case 0: return 20f; 
            case 1: return 50f; 
            case 2: return 100f; 
            default: return 20f; 
        }
    }

    private void HandleIncomingTwinOverrides(string topic, string payload) 
    {
        if (topic != _overrideTopic) return; 

        try 
        {
            string[] commandParts = payload.Split(','); 
            if (commandParts.Length < 2) return; 

            _overrideTargetPercentage = float.Parse(commandParts[0]); 
            _overrideCountdownTimer = float.Parse(commandParts[1]); 
            _isTwinOverrideActive = true; 

            ForceNetworkBroadcast();
        }
        catch (Exception ex) 
        {
            Debug.LogError($"[HVAC OVERRIDE ERROR] Failed to parse command data string: {ex.Message}"); 
        }
    }

    private void PublishJsonTelemetry(float currentPercentage) 
    {
        HVACTelemetryPayload payload = new HVACTelemetryPayload 
        {
            co2 = currentCO2, 
            no = currentNO, 
            operatingPercentage = currentPercentage, 
            smallFanRPM = smallFanMaxRPM * (currentPercentage / 100f), 
            bigFanRPM = bigFanMaxRPM * (currentPercentage / 100f) 
        };

        string jsonString = JsonUtility.ToJson(payload); 

        if (MQTTConnectionManager.Instance != null) 
        {
            MQTTConnectionManager.Instance.PublishTopic("DuoResidence/Amr/Garage/HVAC/Telemetry", jsonString, retain: true);
        }
    }

    private void SubscribeToTwinCommandChannel() { if (MQTTConnectionManager.Instance != null) MQTTConnectionManager.Instance.SubscribeToTopic(_overrideTopic); } 
    private void OnCO2CanvasSliderChanged(float value) { _isUpdatingFromCanvas = true; currentCO2 = value; _lastCO2 = value; _isUpdatingFromCanvas = false; } 
    private void OnNOCanvasSliderChanged(float value) { _isUpdatingFromCanvas = true; currentNO = value; _lastNO = value; _isUpdatingFromCanvas = false; } 
    private int GetCO2Severity(float ppm) { if (ppm < 1000f) return 0; if (ppm < 2000f) return 1; return 2; } 
    private int GetNOSeverity(float ppm) { if (ppm < 25f) return 0; if (ppm < 50f) return 1; return 2; } 
    private void OnDestroy() { if (co2CanvasSlider != null) co2CanvasSlider.onValueChanged.RemoveListener(OnCO2CanvasSliderChanged); if (noCanvasSlider != null) noCanvasSlider.onValueChanged.RemoveListener(OnNOCanvasSliderChanged); MQTTConnectionManager.OnTelemetryMessageReceived -= HandleIncomingTwinOverrides; } 
}