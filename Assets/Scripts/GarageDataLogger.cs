using UnityEngine;
using System;
using System.IO;

public class GarageDataLogger : MonoBehaviour
{
    // A nested structure defining the raw database columns for Excel
    [System.Serializable]
    public class ParkingRecord
    {
        public string licensePlateID;
        public string entryTimestamp;
        public string allocatedSlotID;
        public string exitTimestamp;
    }

    [Header("Storage Configuration")]
    [Tooltip("The name of the Excel-compatible file stored in your project folder.")]
    [SerializeField] private string csvFileName = "DuoResidence_GarageLogs.csv";

    private string fullFilePath;

    void Awake()
    {
        // Establish an absolute file path pointing to your project directory
        fullFilePath = Path.Combine(Application.dataPath, csvFileName);
        
        InitializeCSVFile();
    }

    /// <summary>
    /// Checks the local storage disc. If no logging sheet exists, it generates 
    /// a new file and bakes the essential spreadsheet headers into row 1.
    /// </summary>
    private void InitializeCSVFile()
    {
        try
        {
            if (!File.Exists(fullFilePath))
            {
                // Create the file and open a secure text stream writer to add the columns
                using (StreamWriter writer = new StreamWriter(fullFilePath, false, System.Text.Encoding.UTF8))
                {
                    // Excel recognizes commas as cell dividers. This acts as Row 1.
                    string csvHeader = "License Plate ID,Entry Date & Time,Allocated Slot ID,Exit Date & Time";
                    writer.WriteLine(csvHeader);
                }
                
                Debug.Log($"<color=#00FFCC><b>[Data Logger Initialize]:</b></color> Created fresh spreadsheet matrix asset at: {fullFilePath}");
            }
            else
            {
                Debug.Log($"<color=#FFFF00><b>[Data Logger Initialize]:</b></color> Existing log database located safely. Ready to append telemetry entries.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Data Logger Error] Failed to safely initialize tracking asset file: {ex.Message}");
        }
    }

    /// <summary>
    /// Generates a highly unique pseudo-random license plate identifier string
    /// using a 3-Letter and 4-Number layout paradigm (e.g., "XYZ-8942").
    /// </summary>
    public string GenerateRandomLicensePlate()
    {
        // Define our lookup arrays for the random index selector
        char[] lettersMatrix = "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();
        char[] numbersMatrix = "0123456789".ToCharArray();

        System.Text.StringBuilder plateBuilder = new System.Text.StringBuilder();

        // 1. Sequentially pull 3 random characters from the alphabet array
        for (int i = 0; i < 3; i++)
        {
            int randomIndex = UnityEngine.Random.Range(0, lettersMatrix.Length);
            plateBuilder.Append(lettersMatrix[randomIndex]);
        }

        // Add a clean hyphen separator for spatial legibility inside the database row
        plateBuilder.Append("-");

        // 2. Sequentially pull 4 random characters from the numeric digit array
        for (int i = 0; i < 4; i++)
        {
            int randomIndex = UnityEngine.Random.Range(0, numbersMatrix.Length);
            plateBuilder.Append(numbersMatrix[randomIndex]);
        }

        string finalPlateID = plateBuilder.ToString();
        
        Debug.Log($"<color=#FF00FF><b>[Vehicle ID Engine]:</b></color> Generated unique identity token: {finalPlateID}");
        return finalPlateID;
    }

    /// <summary>
    /// Compiles complete vehicle lifecycle metrics, sanitizes the strings to protect 
    /// CSV architecture, and appends a single row to the Excel-compatible ledger.
    /// </summary>
    public void LogVehicleExit(string plate, string entryTime, string slot, string exitTime)
    {
        try
        {
            // CSV SAFETY GUARD: If any string accidentally contains a raw comma, it shifts 
            // the data into the wrong Excel column. We strip out rogue commas here.
            string sanitizedPlate = plate.Replace(",", " ");
            string sanitizedEntry = entryTime.Replace(",", " ");
            string sanitizedSlot = string.IsNullOrEmpty(slot) ? "UNASSIGNED" : slot.Replace(",", " ");
            string sanitizedExit = exitTime.Replace(",", " ");

            // Combine the cells using standard comma separation syntax
            string csvLine = $"{sanitizedPlate},{sanitizedEntry},{sanitizedSlot},{sanitizedExit}";

            // Open the file with the 'append' parameter set to TRUE to add text to the bottom
            using (StreamWriter writer = new StreamWriter(fullFilePath, true, System.Text.Encoding.UTF8))
            {
                writer.WriteLine(csvLine);
            }

            Debug.Log($"<color=#FF5555><b>[Data Logger Saved]:</b></color> Row committed to Excel for Vehicle: {sanitizedPlate} at Slot: {sanitizedSlot}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Data Logger Error] Failed to append data line to CSV database: {ex.Message}");
        }
    }
}