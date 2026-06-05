using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class CarAgent : MonoBehaviour
{
    [Header("Navigation Matrix")]
    [HideInInspector] public Transform[] allExitPoints; 
    
    [Header("Parking Timers")]
    public float minParkTime = 5f;
    public float maxParkTime = 15f;

    [Header("Bumper Sensors")]
    [SerializeField] private float detectionDistance = 4.0f; 

    private NavMeshAgent _agent;
    private NavMeshObstacle _obstacle; 
    private ParkingSlotController _targetSlot;
    private Vector3 _targetPosition;
    private TrafficSpawner _mySpawner;
    
    private bool _isTrafficBrakingActive = false;

    void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        
        // Ensure the agent handles steering and turning naturally while navigating lanes
        _agent.updateRotation = true; 
        
        StartCoroutine(ParkingRoutine());
    }

    void Update()
    {
        if (_isTrafficBrakingActive && _agent != null && _agent.enabled)
        {
            // Standard front-bumper radar sweeps directly ahead of the car mesh orientation
            Ray forwardRay = new Ray(transform.position + Vector3.up * 0.5f, transform.forward);
            
            if (Physics.Raycast(forwardRay, out RaycastHit hit, detectionDistance))
            {
                if (hit.collider.CompareTag("Vehicle"))
                {
                    CarAgent otherCar = hit.collider.GetComponentInParent<CarAgent>();
                    if (otherCar != null)
                    {
                        if (this.GetEntityId() > otherCar.GetEntityId())
                        {
                            _agent.isStopped = true; 
                            return;
                        }
                        else
                        {
                            _agent.isStopped = false; 
                            return;
                        }
                    }
                    _agent.isStopped = true;
                    return;
                }
            }
            _agent.isStopped = false; 
        }
    }

    public void SetSpawnerReference(TrafficSpawner spawner)
    {
        _mySpawner = spawner;
    }

    private IEnumerator ParkingRoutine()
    {
        // 1. THE BUFFER
        yield return new WaitForSeconds(3f); 

        // 2. Request a slot
        _targetSlot = ParkingManager.Instance.GetRandomAvailableSlot();

        if (_targetSlot == null)
        {
            RouteToNearestExit();
            yield break;
        }

        // 3. Drive to the slot
        _targetPosition = _targetSlot.ReserveAndGetTarget(this); 
        _isTrafficBrakingActive = true; 
        _agent.SetDestination(_targetPosition);
        
        yield return new WaitUntil(() => !_agent.pathPending);

        // PHASE A: Long-Distance Travel Loop
        while (Vector3.Distance(transform.position, _targetPosition) > 2.0f)
        {
            if (!_agent.hasPath && _agent.enabled && !_agent.pathPending)
            {
                _agent.SetDestination(_targetPosition);
            }
            yield return null;
        }

        // THE TRIGGER POINT: Crosses the 2-meter mark to activate the LED debounce timer
        if (_targetSlot != null)
        {
            _targetSlot.StartDebounceTimer();
        }

        // PHASE B: Final Entrance Entry
        float finalManeuverTimer = 0f;
        while (Vector3.Distance(transform.position, _targetPosition) > 0.35f && finalManeuverTimer < 4f)
        {
            finalManeuverTimer += Time.deltaTime;
            yield return null;
        }

        // ======================================================================
        // 4. Park the car (The Snapping & Roadblock Phase)
        // ======================================================================
        _isTrafficBrakingActive = false;
        _agent.isStopped = true;
        _agent.ResetPath();
        
        _agent.enabled = false;

        // FIXED: Performs BOTH position and rotation alignment snaps.
        // This forces the car to turn and lock perfectly flush with the slot, facing the wall.
        transform.position = new Vector3(_targetPosition.x, transform.position.y, _targetPosition.z);
        transform.rotation = _targetSlot.transform.rotation; 

        // Instant roadblock initialization
        _obstacle = gameObject.AddComponent<NavMeshObstacle>();
        _obstacle.carving = true;
        _obstacle.shape = NavMeshObstacleShape.Box;
        
        if (TryGetComponent<BoxCollider>(out var boxCol))
        {
            _obstacle.size = boxCol.size;
            _obstacle.center = boxCol.center;
        }

        float parkDuration = Random.Range(minParkTime, maxParkTime);
        yield return new WaitForSeconds(parkDuration);

        // ======================================================================
        // 5. Leave Slot (Autonomous Reverse Maneuver)
        // ======================================================================
        if (_obstacle != null) 
        {
            Destroy(_obstacle);
        }
        yield return new WaitForFixedUpdate(); 

        ParkingManager.Instance.ReturnSlotToPool(_targetSlot);

        // REVERSE ENGINE CONTROLLER
        Vector3 startPosition = transform.position;
        Vector3 reverseDirection = transform.forward; // Backwards, away from the slot wall baseline
        float safeReverseDistance = 4.5f;

        // Active Proximity Radar Scan to safeguard against narrow aisles or rear barriers
        Ray clearanceRay = new Ray(transform.position + Vector3.up * 0.5f, reverseDirection);
        if (Physics.Raycast(clearanceRay, out RaycastHit barrierHit, 6.0f))
        {
            if (!barrierHit.collider.CompareTag("Vehicle"))
            {
                safeReverseDistance = Mathf.Max(1.0f, barrierHit.distance - 1.2f);
                Debug.Log($"[CarAgent] Rear Radar Sweep: Custom safe reverse depth set to: {safeReverseDistance:F1}m");
            }
        }

        Vector3 reverseTargetPosition = transform.position + (reverseDirection * safeReverseDistance);
        
        float elapsed = 0f;
        float reverseDuration = 2.0f; 

        while (elapsed < reverseDuration)
        {
            Ray rearRay = new Ray(transform.position + Vector3.up * 0.5f, reverseDirection);
            if (Physics.Raycast(rearRay, out RaycastHit hit, detectionDistance))
            {
                if (hit.collider.CompareTag("Vehicle"))
                {
                    NavMeshAgent trafficAgent = hit.collider.GetComponent<NavMeshAgent>();
                    bool isTrafficMoving = true;
                    if (trafficAgent != null && (trafficAgent.isStopped || trafficAgent.velocity.sqrMagnitude < 0.1f))
                    {
                        isTrafficMoving = false; 
                    }

                    if (isTrafficMoving)
                    {
                        yield return null;
                        continue; 
                    }
                }
            }

            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(startPosition, reverseTargetPosition, elapsed / reverseDuration);
            yield return null;
        }
        transform.position = reverseTargetPosition; 

        yield return new WaitForSeconds(0.5f); 

        _agent.enabled = true; 

        RouteToNearestExit();
    }

    private void RouteToNearestExit()
    {
        if (allExitPoints == null || allExitPoints.Length == 0)
        {
            Destroy(gameObject);
            return;
        }

        Transform closestExit = allExitPoints[0];
        float shortestDistance = float.MaxValue;

        foreach (Transform exitNode in allExitPoints)
        {
            if (exitNode == null) continue;
            float evaluationDistance = Vector3.Distance(transform.position, exitNode.position);
            
            if (evaluationDistance < shortestDistance)
            {
                shortestDistance = evaluationDistance;
                closestExit = exitNode;
            }
        }

        if (closestExit != null)
        {
            _isTrafficBrakingActive = true; 
            _agent.isStopped = false;
            _agent.SetDestination(closestExit.position);
            
            StartCoroutine(WaitForDespawnArrival(closestExit));
        }
    }

    private IEnumerator WaitForDespawnArrival(Transform exitNode)
    {
        yield return new WaitUntil(() => !_agent.pathPending);

        while (Vector3.Distance(transform.position, exitNode.position) > 1.5f)
        {
            yield return null;
        }

        if (_mySpawner != null)
        {
            _mySpawner.NotifyCarDespawned();
        }

        Destroy(gameObject);
    }
}