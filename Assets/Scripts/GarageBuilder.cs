using UnityEngine;

public class GarageBuilder : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject parkingSlotPrefab;
    public GameObject pavementPrefab; 
    public GameObject wallPrefab;     

    [Header("Garage Settings")]
    public int slotsPerRow = 15;
    public float slotWidth = 2.5f;
    public float slotDepth = 5f;
    public float laneRoadWidth = 6f;
    
    [Header("Spacing & Architecture")]
    public float slotSpacingGap = 0.5f; 
    public float wallThickness = 0.5f;
    [Tooltip("Matches your updated architectural wall profile.")]
    public float wallHeight = 7.5f; 
    [Tooltip("Extra physical length added to pavements to create a tighter alignment channel for the AI cars.")]
    public float pavementLengthExtension = 0.8f; 

    [Header("Procedural Roof & Slot Mount Settings")]
    public bool buildRoofStructure = true;
    public GameObject roofPrefab;
    public GameObject hangingMountPrefab; 
    [Tooltip("How far down the cuboid mount extends from the ceiling roof line for standard parking slot displays.")]
    public float mountVerticalLength = 0.6f;
    
    [Space(5)]
    [Tooltip("Explicit physical width (X axis) of the entire compound footprint.")]
    public float roofWidth = 55f; 
    [Tooltip("Explicit physical length (Z axis) of the entire compound footprint.")]
    public float roofLength = 81f; 

    [Header("Lane Markings (Hex FFEB04)")]
    public GameObject yellowLinePrefab;
    [Tooltip("The thickness of the painted line on the ground (e.g., 15 centimeters).")]
    public float yellowLineWidth = 0.15f;
    [Tooltip("How far to push the lines away from the pavement edges into the driving lane.")]
    [SerializeField] private float laneLineOffset = 0.2f; 

    [Header("Entrance Overhead Signage (Tweakable Fields)")]
    public GameObject laneSignPrefab;
    public float signPlacementY = 4f; 
    public float signPlacementZ = 40.02f; 
    public float signRotationY = 0f; 

    [Header("Exit LED Signage (Tweakable Fields)")]
    public GameObject exitSignPrefab;
    public float exitSignPlacementY = 4f; 
    public float exitSignPlacementZ = -40.02f; 
    public float exitSignRotationY = 180f; 

    [Header("HVAC Custom Ventilation System")]
    [Tooltip("Drag your large extraction fan prefab here.")]
    public GameObject bigFanPrefab;
    [Tooltip("Drag your custom square-enclosure small fan prefab here.")]
    public GameObject smallFanPrefab;
    
    [Space(5)]
    [Range(0f, 100f)] public float fanOperatingRatingPercentage = 50f;
    public float bigFanMaxRPM = 720f;
    public float smallFanMaxRPM = 1440f;

    [Header("HVAC Ceiling Alignment")]
    [Tooltip("Fine-tune adjustment to keep the small fans flush under the high ceiling.")]
    public float smallFanCeilingOffset = 1.5f;
    [Tooltip("How many small distribution fans you want to spawn down the length of each individual lane road channel.")]
    public int smallFansPerLaneCount = 4;

    // X coordinates for the exact center of each 6m driving aisle
    private float[] aisleCentersX = new float[] { -18.5f, 0f, 18.5f };
    private string[] laneNames = new string[] { "A", "B", "C" };
    private float totalRowLength;

    void Start()
    {
        totalRowLength = (slotsPerRow * slotWidth) + ((slotsPerRow - 1) * slotSpacingGap);
        
        BuildSpacedGarage();
        BuildDividingWalls();
        BuildRoof(); 
        BuildLaneLines(); 
        BuildEntranceSigns(); 
        BuildExitSigns(); 
        
        // Render the upgraded dual-ventilation system
        BuildVentilationSystem();
        LogFanStatus();
    }

    private void LogFanStatus()
    {
        // Debug.Log($"<color=#00FFCC><b>[HVAC System]:</b></color> Fans are operating at {fanOperatingRatingPercentage}% rating.");
    }

    private void BuildVentilationSystem()
    {
        Vector3[] exactBigFanPositions = new Vector3[]
        {
            new Vector3(9.25f, 6.6f, 40f),
            new Vector3(-9.25f, 6.6f, 40f),
            new Vector3(9.25f, 6.6f, -40f),
            new Vector3(-9.25f, 6.6f, -40f)
        };

        if (bigFanPrefab != null)
        {
            for (int i = 0; i < exactBigFanPositions.Length; i++)
            {
                GameObject fanObj = Instantiate(bigFanPrefab, exactBigFanPositions[i], Quaternion.identity, this.transform);
                fanObj.name = $"HVAC_BigFan_Position_{i + 1}";

                BigFanController controller = fanObj.GetComponentInChildren<BigFanController>();
                if (controller != null)
                {
                    controller.maxRPM = bigFanMaxRPM;
                    controller.operatingPercentage = fanOperatingRatingPercentage;
                }
            }
        }

        if (smallFanPrefab != null)
        {
            float halfRowLength = totalRowLength / 2f;
            float smallFanY = wallHeight - smallFanCeilingOffset;

            for (int i = 0; i < aisleCentersX.Length; i++)
            {
                float aisleX = aisleCentersX[i];
                string laneName = laneNames[i];

                for (int j = 0; j < smallFansPerLaneCount; j++)
                {
                    float smallFanZ = halfRowLength - (j * (totalRowLength / (smallFansPerLaneCount - 1)));
                    
                    if (j == 0) smallFanZ -= 4f;
                    if (j == smallFansPerLaneCount - 1) smallFanZ += 4f;

                    Vector3 smallFanPos = new Vector3(aisleX, smallFanY, smallFanZ);

                    bool overlapsWithBigFan = false;
                    foreach (Vector3 bigFanPos in exactBigFanPositions)
                    {
                        if (Vector3.Distance(new Vector3(smallFanPos.x, 0, smallFanPos.z), new Vector3(bigFanPos.x, 0, bigFanPos.z)) < 6f)
                        {
                            overlapsWithBigFan = true;
                            break;
                        }
                    }

                    if (overlapsWithBigFan) continue;

                    GameObject fanObj = Instantiate(smallFanPrefab, smallFanPos, Quaternion.identity, this.transform);
                    fanObj.name = $"HVAC_SmallFan_Lane_{laneName}_Zone_{j}";

                    SmallFanController controller = fanObj.GetComponentInChildren<SmallFanController>();
                    if (controller != null)
                    {
                        controller.maxRPM = smallFanMaxRPM;
                        controller.operatingPercentage = fanOperatingRatingPercentage;
                    }
                }
            }
        }
    }

    private void BuildSpacedGarage()
    {
        BuildRow(laneNames[0], aisleCentersX[0], isRightSide: false, startSlotNumber: 1);
        BuildRow(laneNames[0], aisleCentersX[0], isRightSide: true, startSlotNumber: 21);

        BuildRow(laneNames[1], aisleCentersX[1], isRightSide: false, startSlotNumber: 1);
        BuildRow(laneNames[1], aisleCentersX[1], isRightSide: true, startSlotNumber: 21);

        BuildRow(laneNames[2], aisleCentersX[2], isRightSide: false, startSlotNumber: 1);
        BuildRow(laneNames[2], aisleCentersX[2], isRightSide: true, startSlotNumber: 21);
    }

    private void BuildRow(string laneID, float aisleCenterX, bool isRightSide, int startSlotNumber)
    {
        float offsetX = (laneRoadWidth / 2f) + (slotDepth / 2f);
        float slotX = isRightSide ? aisleCenterX + offsetX : aisleCenterX - offsetX;
        float rotationY = isRightSide ? -90f : 90f;
        float effectiveSlotSpacing = slotWidth + slotSpacingGap;

        for (int i = 0; i < slotsPerRow; i++)
        {
            float slotZ = -(totalRowLength / 2f) + (slotWidth / 2f) + (i * effectiveSlotSpacing);
            string slotID = laneID + (startSlotNumber + i).ToString("D2");
            
            GameObject spawnedSlot = SpawnSlot(slotID, laneID, slotX, slotZ, rotationY);
            ParkingSlotController controller = spawnedSlot.GetComponent<ParkingSlotController>();
            if (controller != null)
            {
                ParkingManager.Instance.RegisterSlot(controller);
            }

            if (buildRoofStructure && hangingMountPrefab != null)
            {
                float mountYPosition = wallHeight - (mountVerticalLength / 2f);
                Vector3 mountPos = new Vector3(slotX, mountYPosition, slotZ);
                GameObject mount = Instantiate(hangingMountPrefab, mountPos, Quaternion.Euler(0, rotationY, 0), this.transform);
                mount.name = "Mount_Bracket_" + slotID;
                mount.transform.localScale = new Vector3(0.4f, mountVerticalLength, 0.4f);
            }

            if (i == 0 && pavementPrefab != null)
            {
                float firstPavementZ = slotZ - (slotWidth / 2f) - (slotSpacingGap / 2f);
                Vector3 pavementPos = new Vector3(slotX, 0.1f, firstPavementZ); 
                GameObject pavement = Instantiate(pavementPrefab, pavementPos, Quaternion.identity, this.transform);
                pavement.name = "Pavement_Start_" + laneID;
                pavement.transform.localScale = new Vector3(slotDepth + pavementLengthExtension, 0.2f, slotSpacingGap);
            }

            if (pavementPrefab != null)
            {
                float pavementZ = slotZ + (slotWidth / 2f) + (slotSpacingGap / 2f);
                Vector3 pavementPos = new Vector3(slotX, 0.1f, pavementZ); 
                GameObject pavement = Instantiate(pavementPrefab, pavementPos, Quaternion.identity, this.transform);
                pavement.name = "Pavement_" + slotID;
                pavement.transform.localScale = new Vector3(slotDepth + pavementLengthExtension, 0.2f, slotSpacingGap);
            }
        }
    }

    private GameObject SpawnSlot(string slotID, string laneID, float x, float z, float rotationY)
    {
        Vector3 position = new Vector3(x, 0, z);
        Quaternion rotation = Quaternion.Euler(0, rotationY, 0);

        // 1. Instantiate the combined prefab (contains slot, dual indicators, and nested sign)
        GameObject slot = Instantiate(parkingSlotPrefab, position, rotation, this.transform);
        slot.name = "Slot_" + slotID;

        // 2. Map slot identities cleanly
        ParkingSlotController controller = slot.GetComponent<ParkingSlotController>();
        if (controller != null)
        {
            controller.slotID = slotID;
            controller.laneID = laneID;
        }

        // ======================================================================
        // UPGRADED NESTED PREFAB SIGN DETECTION
        // ======================================================================
        // Searches recursively down child trees to auto-update TextMeshPro labels
        TMPro.TextMeshPro tmPro = slot.GetComponentInChildren<TMPro.TextMeshPro>();
        if (tmPro != null)
        {
            tmPro.text = slotID;
        }
        else
        {
        // FIXED: Explicitly declared variable type as TextMesh component instead of Mesh
            TextMesh textMesh = slot.GetComponentInChildren<TextMesh>();
            if (textMesh != null)
            {
                textMesh.text = slotID;
            }
        }

        return slot;
    }

    private void BuildDividingWalls()
    {
        float gapCenterX_AB = (aisleCentersX[0] + aisleCentersX[1]) / 2f; 
        float gapCenterX_BC = (aisleCentersX[1] + aisleCentersX[2]) / 2f; 

        Vector3 wallScale = new Vector3(wallThickness, wallHeight, totalRowLength + (2f * slotSpacingGap));

        if (wallPrefab != null)
        {
            GameObject wallAB = Instantiate(wallPrefab, new Vector3(gapCenterX_AB, wallHeight / 2f, 0f), Quaternion.identity, this.transform);
            wallAB.name = "Wall_Between_A_and_B";
            wallAB.transform.localScale = wallScale;

            GameObject wallBC = Instantiate(wallPrefab, new Vector3(gapCenterX_BC, wallHeight / 2f, 0f), Quaternion.identity, this.transform);
            wallBC.name = "Wall_Between_B_and_C";
            wallBC.transform.localScale = wallScale;
        }
    }

    private void BuildRoof()
    {
        if (!buildRoofStructure || roofPrefab == null) return;

        Vector3 roofPos = new Vector3(0f, wallHeight, 0f);
        GameObject roof = Instantiate(roofPrefab, roofPos, Quaternion.identity, this.transform);
        roof.name = "Procedural_Roof_Ceiling";
        roof.transform.localScale = new Vector3(roofWidth, 0.2f, roofLength);
    }

    private void BuildLaneLines()
    {
        if (yellowLinePrefab == null) return;

        float lineLength = totalRowLength + slotSpacingGap;
        float lineY = 0.02f; 

        foreach (float aisleCenterX in aisleCentersX)
        {
            float leftX = aisleCenterX - (laneRoadWidth / 2f) + laneLineOffset;
            Vector3 leftPos = new Vector3(leftX, lineY, 0f);
            GameObject leftLine = Instantiate(yellowLinePrefab, leftPos, Quaternion.identity, this.transform);
            leftLine.name = $"YellowLine_Left_Lane_{laneNames[System.Array.IndexOf(aisleCentersX, aisleCenterX)]}";
            leftLine.transform.localScale = new Vector3(yellowLineWidth, 0.01f, lineLength);

            float rightX = aisleCenterX + (laneRoadWidth / 2f) - laneLineOffset;
            Vector3 rightPos = new Vector3(rightX, lineY, 0f);
            GameObject rightLine = Instantiate(yellowLinePrefab, rightPos, Quaternion.identity, this.transform);
            rightLine.name = $"YellowLine_Right_Lane_{laneNames[System.Array.IndexOf(aisleCentersX, aisleCenterX)]}";
            rightLine.transform.localScale = new Vector3(yellowLineWidth, 0.01f, lineLength);
        }
    }

    private void BuildEntranceSigns()
    {
        if (laneSignPrefab == null) return;

        for (int i = 0; i < aisleCentersX.Length; i++)
        {
            Vector3 spawnPosition = new Vector3(aisleCentersX[i], signPlacementY, signPlacementZ);
            GameObject signInstance = Instantiate(laneSignPrefab, spawnPosition, Quaternion.Euler(0f, signRotationY, 0f), this.transform);
            signInstance.name = $"EntranceSign_Lane_{laneNames[i]}";

            LaneSign signController = signInstance.GetComponent<LaneSign>();
            if (signController != null)
            {
                signController.laneID = laneNames[i];
                signController.StartMapping(); 
            }
        }
    }

    private void BuildExitSigns()
    {
        if (exitSignPrefab == null) return;

        for (int i = 0; i < aisleCentersX.Length; i++)
        {
            Vector3 spawnPosition = new Vector3(aisleCentersX[i], exitSignPlacementY, exitSignPlacementZ);
            GameObject signInstance = Instantiate(exitSignPrefab, spawnPosition, Quaternion.Euler(0f, exitSignRotationY, 0f), this.transform);
            signInstance.name = $"ExitSign_Lane_{laneNames[i]}";
        }
    }
}