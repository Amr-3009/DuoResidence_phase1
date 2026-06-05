using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class GarageServerConfigMenu : MonoBehaviour
{
    [Header("Hidden UI Overlay Panel")]
    [SerializeField] private GameObject configPanelContainer;
    [SerializeField] private KeyCode toggleKey = KeyCode.F1;

    [Header("Environmental Sensor Overrides")]
    [SerializeField] private Slider co2Slider;
    [SerializeField] private TextMeshProUGUI co2ValueLabel;
    [SerializeField] private Slider noSlider;
    [SerializeField] private TextMeshProUGUI noValueLabel;

    // Safety flags to prevent infinite listener feedback loops
    private bool _isSettingValueInternally = false;

    // Hardcoded network defaults preserved so your CCTV streaming server doesn't break
    public static string MqttBrokerIp { get; private set; } = "127.0.0.1";
    public static int MqttPort { get; private set; } = 1883;
    public static int StreamingPort { get; private set; } = 8555;
    public static int StreamCompressionQuality { get; private set; } = 75;

    private void Start()
    {
        // Start panel hidden
        if (configPanelContainer != null)
            configPanelContainer.SetActive(false);

        // When the user drags the sliders, push the values into the active simulation instantly
        co2Slider.onValueChanged.AddListener((val) => {
            if (_isSettingValueInternally) return;
            if (AirQualityManager.Instance != null)
                AirQualityManager.Instance.InjectCO2FromMenu(val);
        });

        noSlider.onValueChanged.AddListener((val) => {
            if (_isSettingValueInternally) return;
            if (AirQualityManager.Instance != null)
                AirQualityManager.Instance.InjectNOFromMenu(val);
        });
    }

    private void Update()
    {
        // Toggle menu view with F1
        if (Input.GetKeyDown(toggleKey))
        {
            if (configPanelContainer != null)
                configPanelContainer.SetActive(!configPanelContainer.activeSelf);
        }

        // If the secret menu is actively open on your screen, map the fields bidirectionally
        if (configPanelContainer != null && configPanelContainer.activeSelf && AirQualityManager.Instance != null)
        {
            _isSettingValueInternally = true;

            // Only force the slider handle position if your mouse isn't actively dragging it!
            if (EventSystem.current == null || EventSystem.current.currentSelectedGameObject != co2Slider.gameObject)
            {
                co2Slider.value = AirQualityManager.Instance.GetCO2();
            }
            co2ValueLabel.text = $"CO2 Level: {AirQualityManager.Instance.GetCO2():0} ppm";

            // Only force the NO handle position if your mouse isn't actively dragging it!
            if (EventSystem.current == null || EventSystem.current.currentSelectedGameObject != noSlider.gameObject)
            {
                noSlider.value = AirQualityManager.Instance.GetNO();
            }
            noValueLabel.text = $"NO Level: {AirQualityManager.Instance.GetNO():0.0} ppm";

            _isSettingValueInternally = false;
        }
    }
}