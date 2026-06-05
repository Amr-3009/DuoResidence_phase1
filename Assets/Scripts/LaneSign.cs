using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class LaneSign : MonoBehaviour
{
    [Header("UI Component Anchors")]
    [SerializeField] private TextMeshProUGUI signTextMesh;

    [Header("Lane Settings")]
    [Tooltip("Type 'A', 'B', or 'C' here if you are placing this sign manually in the scene window.")]
    public string laneID;

    private List<ParkingSlotController> _associatedSlots = new List<ParkingSlotController>();
    private int _maxCapacity = 0;

    void Start()
    {
        if (!string.IsNullOrEmpty(laneID) && _associatedSlots.Count == 0)
        {
            StartMapping();
        }
    }

    public void StartMapping()
    {
        StartCoroutine(MapLaneGridDelayed());
    }

    private IEnumerator MapLaneGridDelayed()
    {
        yield return null;

        _associatedSlots.Clear();
        
        // FIXED: Removed FindObjectsSortMode to clear the deprecation warning
        ParkingSlotController[] allGarageSlots = FindObjectsByType<ParkingSlotController>(FindObjectsInactive.Exclude);
        
        foreach (ParkingSlotController slot in allGarageSlots)
        {
            if (slot.laneID == laneID)
            {
                _associatedSlots.Add(slot);
            }
        }

        _maxCapacity = _associatedSlots.Count;
        RefreshSignageDisplay();

        ParkingSlotController.OnSlotStateChanged += RefreshSignageDisplay;
    }

    private void OnDestroy()
    {
        ParkingSlotController.OnSlotStateChanged -= RefreshSignageDisplay;
    }

    private void RefreshSignageDisplay()
    {
        int occupiedCount = 0;

        foreach (ParkingSlotController slot in _associatedSlots)
        {
            if (!slot.IsAvailable())
            {
                occupiedCount++;
            }
        }

        signTextMesh.text = "---------------\n" +
                            $"Lane {laneID}\n" +
                            $"Occupancy {occupiedCount} / {_maxCapacity}\n" +
                            "____________";
    }
}