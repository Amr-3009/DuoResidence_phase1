using UnityEngine;
// NEW INPUT SYSTEM API REQUIREMENT
using UnityEngine.InputSystem;

public class SpectatorController : MonoBehaviour
{
    [Header("Flight Dynamics")]
    [SerializeField] private float moveSpeed = 12f;
    [SerializeField] private float fastMoveSpeed = 30f; // Hold Left Shift to sprint
    [Tooltip("New Input System uses pixel deltas, so keep this sensitivity value low (e.g., 0.05 - 0.2)")]
    [SerializeField] private float lookSensitivity = 0.1f;

    private float _rotationX = 0f;
    private float _rotationY = 0f;
    private bool _isLookingActive = false;

    void Start()
    {
        // Start with a free cursor to easily click on the Canvas UI sliders immediately
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Initialize tracking variables with the current starting spawn angles
        Vector3 currentRotation = transform.localEulerAngles;
        _rotationX = currentRotation.y;
        _rotationY = currentRotation.x;
    }

    void Update()
    {
        HandleCameraRotationMode();
        HandleSpectatorTranslation();
    }

    /// <summary>
    /// Swaps input focus via Right-Click using the New Input System direct API polling.
    /// </summary>
    private void HandleCameraRotationMode()
    {
        if (Mouse.current == null) return;

        // HOLD RIGHT-CLICK: Hide cursor and enable flight look mechanics
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            _isLookingActive = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        
        // RELEASE RIGHT-CLICK: Free the cursor to interact with UI Canvas sliders
        if (Mouse.current.rightButton.wasReleasedThisFrame)
        {
            _isLookingActive = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (_isLookingActive)
        {
            // Read raw mouse pixel delta values directly from the hardware stream
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();

            _rotationX += mouseDelta.x * lookSensitivity;
            
            // In the New Input System, mouse delta Y is positive moving UP, so subtract it
            _rotationY -= mouseDelta.y * lookSensitivity;
            _rotationY = Mathf.Clamp(_rotationY, -85f, 85f);

            transform.localEulerAngles = new Vector3(_rotationY, _rotationX, 0f);
        }
    }

    /// <summary>
    /// Handles mathematical grid translation, bypassing physical wall and vehicle colliders.
    /// </summary>
    private void HandleSpectatorTranslation()
    {
        if (Keyboard.current == null) return;

        // Boost speed if the Left Shift key is actively held down
        float activeSpeed = Keyboard.current.leftShiftKey.isPressed ? fastMoveSpeed : moveSpeed;

        Vector3 moveDirection = Vector3.zero;

        // Map Keyboard directions
        if (Keyboard.current.wKey.isPressed) moveDirection += transform.forward;
        if (Keyboard.current.sKey.isPressed) moveDirection -= transform.forward;
        if (Keyboard.current.dKey.isPressed) moveDirection += transform.right;
        if (Keyboard.current.aKey.isPressed) moveDirection -= transform.right;
        
        // Vertical elevation: E to Ascend, Q to Descend
        if (Keyboard.current.eKey.isPressed) moveDirection += transform.up;
        if (Keyboard.current.qKey.isPressed) moveDirection -= transform.up;

        // Normalize vector inputs so diagonal flight doesn't give a random speed boost
        if (moveDirection.sqrMagnitude > 0.01f)
        {
            transform.position += moveDirection.normalized * activeSpeed * Time.deltaTime;
        }
    }
}