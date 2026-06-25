using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UmbrellaUIController : MonoBehaviour
{
    [Header("Core Engine Link")]
    [SerializeField] private UmbrellaControllerA umbrellaEngine;

    [Header("UI Visibility Elements")]
    [Tooltip("The main container background panel containing all sliders and text elements.")]
    [SerializeField] private GameObject dashboardPanel;
    
    [Tooltip("The keyboard key used to toggle the dashboard visibility.")]
    [SerializeField] private KeyCode toggleKey = KeyCode.U;

    [Header("Wind UI Mapping")]
    [SerializeField] private Slider windSlider;
    [SerializeField] private TMP_Text windMetricsText;

    [Header("Rain UI Mapping")]
    [SerializeField] private Slider rainSlider;
    [SerializeField] private TMP_Text rainMetricsText;

    [Header("Solar UI Mapping")]
    [SerializeField] private Slider solarSlider;
    [SerializeField] private TMP_Text solarMetricsText;

    void Start()
    {
        if (umbrellaEngine == null)
        {
            umbrellaEngine = GetComponentInParent<UmbrellaControllerA>();
        }

        if (umbrellaEngine != null)
        {
            windSlider.value = umbrellaEngine.windSpeed;
            rainSlider.value = umbrellaEngine.rainIntensity;
            solarSlider.value = umbrellaEngine.solarIrradiance;

            windSlider.onValueChanged.AddListener(OnWindSliderChanged);
            rainSlider.onValueChanged.AddListener(OnRainSliderChanged);
            solarSlider.onValueChanged.AddListener(OnSolarSliderChanged);

            UpdateAllMetricsText();
        }
        else
        {
            Debug.LogError("[Umbrella UI] Missing link to UmbrellaControllerA engine core component!");
        }

        // Optional: Ensure the panel starts active on launch so the user knows it exists
        if (dashboardPanel != null)
        {
            dashboardPanel.SetActive(true);
        }
        // FIX: The panel now explicitly starts HIDDEN on launch
        if (dashboardPanel != null)
        {
            dashboardPanel.SetActive(false);
        }
    }

    void Update()
    {
        // Continuous input listener to intercept the hotkey press
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleDashboardVisibility();
        }
    }

    private void ToggleDashboardVisibility()
    {
        if (dashboardPanel != null)
        {
            // Flipped boolean operation: if it's active, hide it. If it's hidden, show it.
            bool currentStatus = dashboardPanel.activeSelf;
            dashboardPanel.SetActive(!currentStatus);
            
            Debug.Log($"[Dashboard Interface] Visibility state shifted. Panel Active: {!currentStatus}");
        }
    }

    private void OnWindSliderChanged(float val)
    {
        umbrellaEngine.windSpeed = val;
        UpdateWindText(val);
    }

    private void OnRainSliderChanged(float val)
    {
        umbrellaEngine.rainIntensity = val;
        UpdateRainText(val);
    }

    private void OnSolarSliderChanged(float val)
    {
        umbrellaEngine.solarIrradiance = val;
        UpdateSolarText(val);
    }

    private void UpdateAllMetricsText()
    {
        UpdateWindText(umbrellaEngine.windSpeed);
        UpdateRainText(umbrellaEngine.rainIntensity);
        UpdateSolarText(umbrellaEngine.solarIrradiance);
    }

    private void UpdateWindText(float value)
    {
        windMetricsText.text = $"Current: {value:F1} m/s  (Threshold: {umbrellaEngine.GetWindLimit():F1} m/s)";
    }

    private void UpdateRainText(float value)
    {
        rainMetricsText.text = $"Current: {value:F0}%  (Threshold: {umbrellaEngine.GetRainLimit():F0}%)";
    }

    private void UpdateSolarText(float value)
    {
        solarMetricsText.text = $"Current: {value:F0} W/m²  (Threshold: {umbrellaEngine.GetSolarLimit():F0} W/m²)";
    }
}