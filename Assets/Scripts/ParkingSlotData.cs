using System;

[Serializable]
public class ParkingSlotData
{
    public string slotID;
    public string laneID;
    public bool isOccupied;
    public string status;

    public ParkingSlotData(string slotID, string laneID)
    {
        this.slotID    = slotID;
        this.laneID    = laneID;
        this.isOccupied = false;
        this.status    = "VACANT";
    }
}