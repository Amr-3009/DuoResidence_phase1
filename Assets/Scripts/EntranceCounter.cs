using UnityEngine;

public class EntranceCounter : MonoBehaviour
{
    [Header("Traffic Metrics")]
    [SerializeField] private int totalCarsEntered = 0;

    [Header("MQTT Configuration")]
    private string topic = "DuoResidence/Amr/Garage/Traffic/EntranceCount";

    private void Start()
    {
        // Reset the counter on the cloud broker at the start of a new simulation session
        PublishCount();
    }

    private void OnTriggerEnter(Collider other)
    {
        // Adjust the tag check to match whatever tag your vehicle prefabs use (e.g., "Car", "Agent", "Vehicle")
        if (other.CompareTag("Vehicle") || other.GetComponentInParent<CarAgent>() != null)
        {
            totalCarsEntered++;
            Debug.Log($"<color=#00BBFF><b>[Traffic Flow]:</b></color> New vehicle detected! Total inbound traffic: {totalCarsEntered}");
            
            // Blast the updated raw number to the cloud network
            PublishCount();
        }
    }

    private void PublishCount()
    {
        if (MQTTConnectionManager.Instance != null)
        {
            MQTTConnectionManager.Instance.PublishTopic(topic, totalCarsEntered.ToString(), retain: true);
        }
    }
}