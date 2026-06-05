using System.Collections.Generic;
using UnityEngine;

public class ParkingManager : MonoBehaviour
{
    // Singleton Accessor
    public static ParkingManager Instance { get; private set; }

    [Header("Garage Inventory")]
    [Tooltip("Leave empty to automatically detect all slots in the scene at startup, or assign them manually.")]
    [SerializeField] private List<ParkingSlotController> allSlots = new List<ParkingSlotController>();

    private void Awake()
    {
        // Establish the Singleton instance
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // If the list wasn't manually filled out in the Inspector, find existing scene slots automatically
        if (allSlots.Count == 0)
        {
            allSlots.AddRange(FindObjectsByType<ParkingSlotController>(FindObjectsInactive.Include));
        }
    }

    /// <summary>
    /// Dynamically registers a procedurally generated parking slot at runtime (called by GarageBuilder.cs).
    /// </summary>
    public void RegisterSlot(ParkingSlotController slot)
    {
        if (slot != null && !allSlots.Contains(slot))
        {
            allSlots.Add(slot);
        }
    }

    /// <summary>
    /// Searches the entire garage matrix for currently unreserved spaces and selects one at random.
    /// </summary>
    public ParkingSlotController GetRandomAvailableSlot()
    {
        List<ParkingSlotController> availableSlots = new List<ParkingSlotController>();

        // Filter out slots that are completely free
        foreach (var slot in allSlots)
        {
            if (slot != null && slot.IsAvailable())
            {
                availableSlots.Add(slot);
            }
        }

        // If none are free, return null so the car agent can safely reroute to an exit
        if (availableSlots.Count == 0)
        {
            return null;
        }

        // Pick and return a random available spot
        int randomIndex = Random.Range(0, availableSlots.Count);
        return availableSlots[randomIndex];
    }

    /// <summary>
    /// Safely releases a slot back into the pool and triggers its visual LED conversion.
    /// </summary>
    public void ReturnSlotToPool(ParkingSlotController slot)
    {
        if (slot != null)
        {
            slot.ReleaseSlot(); // Crucial: This explicitly turns the physical LED back to GREEN
            Debug.Log($"[ParkingManager] Slot {slot.slotID} has been returned to the pool.");
        }
    }
}