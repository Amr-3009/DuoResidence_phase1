using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class TrafficSpawner : MonoBehaviour
{
    public enum SpawnMode { ContinuousMaxActive, FixedTotalAmount }

    [Header("Spawn Mode Configuration")]
    [SerializeField] private SpawnMode currentSpawnMode = SpawnMode.ContinuousMaxActive;
    [SerializeField] private int maxActiveCars = 15;   
    [SerializeField] private int fixedTotalAmount = 25; 

    [Header("Spawn Points")]
    [SerializeField] private Transform[] entryPoints; 
    [SerializeField] private Transform[] exitPoints;  

    [Header("Vehicle Visual Variants")]
    [SerializeField] private GameObject[] carPrefabs; 

    [Header("Traffic Flow Control")]
    [SerializeField] private float spawnDelay = 4.0f;  
    [SerializeField] private float spawnClearanceRadius = 4.5f; 

    [Header("Asset Compatibility Toggles")]
    [Tooltip("Turn ON for Kenney models that look backward. Turn OFF for standard test cars.")]
    [SerializeField] private bool flipAssetOrientation180 = false;
    
    [Tooltip("Turn ON to let the script automatically calculate box sizes for raw meshes. Turn OFF if your prefab already has a pre-sized collider/agent attached.")]
    [SerializeField] private bool autoConfigurePhysicsFromMesh = true;

    [Header("Programmatic Car Settings (Only active if Auto-Configure is True)")]
    [SerializeField] private float vehicleVisualScale = 1.0f; 
    [SerializeField] private float vehicleSpeed = 4.5f;
    [SerializeField] private float vehicleAcceleration = 4.0f;
    [SerializeField] private float vehicleAngularSpeed = 360f; 

    private int _currentActiveCars = 0;
    private int _totalCarsSpawnedSoFar = 0; 

    void Start()
    {
        if (entryPoints.Length == 0 || carPrefabs.Length == 0)
        {
            Debug.LogError("[TrafficSpawner] Please assign Entry Points and Car Prefabs in the Inspector!");
            return;
        }

        StartCoroutine(TrafficLoop());
    }

    private IEnumerator TrafficLoop()
    {
        while (true)
        {
            bool canSpawn = false;

            if (currentSpawnMode == SpawnMode.ContinuousMaxActive)
            {
                if (_currentActiveCars < maxActiveCars)
                {
                    canSpawn = true;
                }
            }
            else if (currentSpawnMode == SpawnMode.FixedTotalAmount)
            {
                if (_totalCarsSpawnedSoFar >= fixedTotalAmount)
                {
                    Debug.Log($"[TrafficSpawner] Target batch of {fixedTotalAmount} vehicles successfully generated. Spawner deactivated.");
                    yield break; 
                }

                if (_currentActiveCars < maxActiveCars)
                {
                    canSpawn = true;
                }
            }

            if (canSpawn)
            {
                int randomLane = Random.Range(0, entryPoints.Length);
                Transform spawnTarget = entryPoints[randomLane];

                if (IsSpawnZoneClear(spawnTarget))
                {
                    GameObject selectedCarPrefab = carPrefabs[Random.Range(0, carPrefabs.Length)];
                    
                    Quaternion finalRotation = spawnTarget.rotation;
                    if (flipAssetOrientation180)
                    {
                        finalRotation *= Quaternion.Euler(0f, 180f, 0f);
                    }

                    GameObject spawnedCar = Instantiate(selectedCarPrefab, spawnTarget.position, finalRotation);
                    
                    if (autoConfigurePhysicsFromMesh)
                    {
                        spawnedCar.transform.localScale = Vector3.one * vehicleVisualScale;
                    }
                    
                    _currentActiveCars++;
                    _totalCarsSpawnedSoFar++; 

                    ConfigureVehicleNavigation(spawnedCar);
                }
            }

            yield return new WaitForSeconds(spawnDelay);
        }
    }

    private bool IsSpawnZoneClear(Transform spawnPoint)
    {
        Collider[] colliders = Physics.OverlapSphere(spawnPoint.position, spawnClearanceRadius);
        foreach (var col in colliders) 
        {
            if (col.CompareTag("Vehicle"))
            {
                return false; 
            }
        }
        return true; 
    }

    private void ConfigureVehicleNavigation(GameObject car)
    {
        car.tag = "Vehicle";

        // ======================================================================
        // BRANCH 1: PURE PREFAB RESPECT (Zero Programmatic Additions)
        // ======================================================================
        if (!autoConfigurePhysicsFromMesh)
        {
            CarAgent customScript = car.GetComponent<CarAgent>();
            
            if (customScript == null)
            {
                Debug.LogError($"[TrafficSpawner] Spawned prefab '{car.name}' is missing a 'CarAgent' component! Navigation cannot start.");
                return;
            }

            // Link critical runtime network variables
            customScript.allExitPoints = exitPoints;
            customScript.SetSpawnerReference(this);
            return;
        }

        // ======================================================================
        // BRANCH 2: AUTO-CONSTRUCT HOUSINGS (Kept untouched for Kenney meshes)
        // ======================================================================
        Bounds visualBounds = new Bounds(car.transform.position, Vector3.zero);
        Renderer[] renderers = car.GetComponentsInChildren<Renderer>();
        bool boundsInitialized = false;

        foreach (Renderer rend in renderers)
        {
            if (!boundsInitialized)
            {
                visualBounds = rend.bounds;
                boundsInitialized = true;
            }
            else
            {
                visualBounds.Encapsulate(rend.bounds);
            }
        }

        Vector3 localSize = car.transform.InverseTransformVector(visualBounds.size);
        localSize.x = Mathf.Abs(localSize.x);
        localSize.y = Mathf.Abs(localSize.y);
        localSize.z = Mathf.Abs(localSize.z);

        Vector3 localCenter = car.transform.InverseTransformPoint(visualBounds.center);
        float calculatedBaseOffset = Mathf.Abs(localCenter.y - (localSize.y / 2f));

        BoxCollider boxCol = car.GetComponent<BoxCollider>();
        if (boxCol == null) boxCol = car.AddComponent<BoxCollider>();
        boxCol.size = localSize;
        boxCol.center = car.transform.InverseTransformPoint(visualBounds.center);

        Rigidbody rb = car.GetComponent<Rigidbody>();
        if (rb == null) rb = car.AddComponent<Rigidbody>();
        rb.isKinematic = true; 

        NavMeshAgent agent = car.GetComponent<NavMeshAgent>();
        if (agent == null) agent = car.AddComponent<NavMeshAgent>();
        
        agent.speed = vehicleSpeed;
        agent.acceleration = vehicleAcceleration;
        agent.angularSpeed = vehicleAngularSpeed;
        agent.stoppingDistance = 0.1f;
        agent.radius = (localSize.x / 2f) * 1.1f; 
        agent.height = localSize.y;
        
        agent.baseOffset = calculatedBaseOffset;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

        NavMeshObstacle obstacle = car.GetComponent<NavMeshObstacle>();
        if (obstacle == null) obstacle = car.AddComponent<NavMeshObstacle>();
        obstacle.carving = true;
        obstacle.enabled = false; 

        CarAgent carScript = car.GetComponent<CarAgent>();
        if (carScript == null) carScript = car.AddComponent<CarAgent>();

        carScript.allExitPoints = exitPoints;
        carScript.SetSpawnerReference(this);
    }

    public void NotifyCarDespawned()
    {
        _currentActiveCars--;
        if (_currentActiveCars < 0) _currentActiveCars = 0;
    }

    private void OnDrawGizmosSelected()
    {
        if (entryPoints == null) return;
        Gizmos.color = Color.cyan;
        foreach (var pt in entryPoints)
        {
            if (pt != null) Gizmos.DrawWireSphere(pt.position, spawnClearanceRadius);
        }
    }
}