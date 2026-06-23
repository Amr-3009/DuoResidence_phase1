using UnityEngine;
using System;

public class EntranceCounter : MonoBehaviour
{
    [Header("Traffic Metrics")]
    [SerializeField] private int totalCarsEntered = 0;

    [Header("Logging Integration")]
    [SerializeField] private GarageDataLogger dataLogger;

    private string topic = "DuoResidence/Amr/Garage/Traffic/EntranceCount";

    private void Start()
    {
        PublishCount();
    }

    private void OnTriggerEnter(Collider other)
    {
        CarAgent agent = other.GetComponentInParent<CarAgent>();

        if (agent != null)
        {
            // Fetch the passport component that was guaranteed to spawn on birth
            VehicleIdentity carId = agent.GetComponent<VehicleIdentity>();

            if (carId == null)
            {
                // Fallback generation just in case an external entity slips past
                carId = agent.gameObject.AddComponent<VehicleIdentity>();
            }

            // Verification Safety: Check if this car has already been processed by the entrance gate
            if (string.IsNullOrEmpty(carId.entryTimestamp))
            {
                totalCarsEntered++;

                // Populate tracking timestamps without wiping out the preset slot ID
                if (dataLogger != null)
                {
                    carId.licensePlateID = dataLogger.GenerateRandomLicensePlate();
                }
                else
                {
                    carId.licensePlateID = "TEMP-" + UnityEngine.Random.Range(1000, 9999).ToString();
                }

                carId.entryTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                Debug.Log($"<color=#00BBFF><b>[Entrance Stamped]</b></color> Car Plate: {carId.licensePlateID} | Saved Target Slot ID: {carId.assignedSlotID}");

                PublishCount();
            }
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