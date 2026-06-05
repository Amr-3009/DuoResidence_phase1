using System.Collections;
using UnityEngine;

public class ParkingSlotController : MonoBehaviour
{
    [Header("Slot Identity")]
    public string slotID;
    public string laneID; // Required for GarageBuilder.cs compatibility

    [Header("Physical LED Mesh Renderers")]
    [SerializeField] private MeshRenderer greenLedRenderer; 
    [SerializeField] private MeshRenderer redLedRenderer; 

    [Header("Industrial LED Materials")]
    [SerializeField] private Material greenOnMaterial;    // Emissive glowing green
    [SerializeField] private Material greenOffMaterial;   // Dark translucent unlit green
    [SerializeField] private Material redOnMaterial;      // Emissive glowing red
    [SerializeField] private Material redOffMaterial;     // Dark translucent unlit red

    [Header("Dynamic Lighting Engine")]
    [SerializeField] private Light greenPointLight;
    [SerializeField] private Light redPointLight;

    [Header("Debounce Settings")]
    [Tooltip("How many seconds the assigned car must sit inside the zone before turning the light RED.")]
    [SerializeField] private float debounceDelay = 0.75f;

    [Header("Initialization Settings")]
    [Tooltip("Delay in seconds before broadcasting the startup state to ensure the map builder is finished.")]
    [SerializeField] private float setupNetworkDelay = 1.5f;

    // Global event tracker that entrance signs can listen to
    public static System.Action OnSlotStateChanged;

    private bool _isReserved = false;
    private Coroutine _debounceCoroutine;

    IEnumerator Start()
    {
        SetLEDColor(true); // Default to green on startup
        
        // 1. PAUSE execution right here to let the GarageBuilder finish spawning everything
        yield return new WaitForSeconds(setupNetworkDelay);
        
        // 2. NETWORK STAGGER: Add a tiny random window (0 to 2 seconds) 
        // This spreads out the 120 messages so they don't choke the MQTT client buffer
        yield return new WaitForSeconds(Random.Range(0f, 2.0f));

        // 3. Now that the layout is fully baked, trigger the initial visual and network sync
        SetLEDColor(true); 
    }

    public Vector3 ReserveAndGetTarget(CarAgent car)
    {
        _isReserved = true;
        return transform.position;
    }

    public void StartDebounceTimer()
    {
        if (_debounceCoroutine == null)
        {
            _debounceCoroutine = StartCoroutine(DebounceParkingRoutine());
        }
    }

    public void ReleaseSlot()
    {
        _isReserved = false;

        if (_debounceCoroutine != null)
        {
            StopCoroutine(_debounceCoroutine);
            _debounceCoroutine = null;
        }

        SetLEDColor(true); 
    }

    public bool IsAvailable()
    {
        return !_isReserved;
    }

    private IEnumerator DebounceParkingRoutine()
    {
        yield return new WaitForSeconds(debounceDelay);
        SetLEDColor(false); // Turn RED early while still parking!
        _debounceCoroutine = null;
    }

    private void SetLEDColor(bool available)
    {
        // --- SWAP INDUSTRIAL PHYSICAL LED CYLINDER MATERIALS & POINT LIGHT STATES ---
        if (available)
        {
            // VACANT STATE: Green active, Red unlit but dimly colored
            if (greenLedRenderer != null) greenLedRenderer.material = greenOnMaterial;
            if (redLedRenderer != null) redLedRenderer.material = redOffMaterial;

            if (greenPointLight != null) greenPointLight.enabled = true;
            if (redPointLight != null) redPointLight.enabled = false;
        }
        else
        {
            // OCCUPIED STATE: Red active, Green unlit but dimly colored
            if (greenLedRenderer != null) greenLedRenderer.material = greenOffMaterial;
            if (redLedRenderer != null) redLedRenderer.material = redOnMaterial;

            if (greenPointLight != null) greenPointLight.enabled = false;
            if (redPointLight != null) redPointLight.enabled = true;
        }

        // ======================================================================
        // PHASE 1 - STEP 3: INTEGRATED MQTT TELEMETRY BROADCAST
        // ======================================================================
        if (MQTTConnectionManager.Instance != null && !string.IsNullOrEmpty(slotID))
        {
            string topic = $"DuoResidence/Amr/Garage/Slots/{laneID}/{slotID}";
            string payload = available ? $"Lane {laneID} - Slot {slotID}: IS VACANT" : $"Lane {laneID} - Slot {slotID}: IS OCCUPIED";
            
            // Retain flag is true so late-connecting apps instantly fetch the layout map state
            MQTTConnectionManager.Instance.PublishTopic(topic, payload, retain: true);
        }

        // Alert all entrance signage displays to recalculate their metrics instantly
        OnSlotStateChanged?.Invoke();
    }
}