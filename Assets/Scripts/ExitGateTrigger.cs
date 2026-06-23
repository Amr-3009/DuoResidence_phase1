using UnityEngine;
using System;

public class ExitGateTrigger : MonoBehaviour
{
    [Header("Logging Integration")]
    [SerializeField] private GarageDataLogger dataLogger;

    private void OnTriggerEnter(Collider other)
    {
        // Find the individual CarAgent passing through the exit gate
        CarAgent agent = other.GetComponentInParent<CarAgent>();

        if (agent != null)
        {
            // Pull the identity component from that specific vehicle
            VehicleIdentity passport = agent.GetComponent<VehicleIdentity>();

            if (passport != null)
            {
                string exitTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                if (dataLogger != null)
                {
                    dataLogger.LogVehicleExit(
                        passport.licensePlateID,
                        passport.entryTimestamp,
                        passport.assignedSlotID,
                        exitTimestamp
                    );
                }

                // Destroy the individual car game object safely
                Destroy(agent.gameObject);
            }
            else
            {
                // Backup destruction fallback to prevent bumper-stuck traffic jams
                Destroy(agent.gameObject);
            }
        }
    }
}