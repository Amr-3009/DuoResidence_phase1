using UnityEngine;
using UnityEngine.InputSystem;

public class SpectatorTargetMover : MonoBehaviour
{
    [Header("Flight Dynamics")]
    [SerializeField] private float moveSpeed = 12f;
    [SerializeField] private float sprintSpeed = 30f; // Activated by holding Left Shift
    [SerializeField] private float lookSensitivity = 0.1f;

    private float _rotationX = 0f;
    private float _rotationY = 0f;
    private bool _isLookingActive = false;

    void Start()
    {
        // Start with a completely unlocked, visible cursor for UI interaction
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Sync initial tracking variables with your Cube's current angles
        Vector3 currentRotation = transform.localEulerAngles;
        _rotationX = currentRotation.y;
        _rotationY = currentRotation.x;
    }

    void Update()
    {
        HandleRotationInput();
        HandleTranslationInput();
    }

    /// <summary>
    /// Tracks mouse movement to turn the cube asset only while holding down Right-Click.
    /// </summary>
    private void HandleRotationInput()
    {
        if (Mouse.current == null) return;

        // Capture Right-Click Down: Hide pointer and engage looking matrix
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            _isLookingActive = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        
        // Capture Right-Click Up: Free pointer to tweak Canvas sliders instantly
        if (Mouse.current.rightButton.wasReleasedThisFrame)
        {
            _isLookingActive = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (_isLookingActive)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();

            _rotationX += mouseDelta.x * lookSensitivity;
            _rotationY -= mouseDelta.y * lookSensitivity; // Subtract to maintain standard axis tracking
            _rotationY = Mathf.Clamp(_rotationY, -85f, 85f);

            transform.localEulerAngles = new Vector3(_rotationY, _rotationX, 0f);
        }
    }

    /// <summary>
    /// Translates the physical coordinate position of the target cube, completely ignoring grid geometry.
    /// </summary>
    private void HandleTranslationInput()
    {
        if (Keyboard.current == null) return;

        // Double movement speed if Left Shift is held down
        float activeSpeed = Keyboard.current.leftShiftKey.isPressed ? sprintSpeed : moveSpeed;
        Vector3 moveVector = Vector3.zero;

        // Basic WASD Mapping
        if (Keyboard.current.wKey.isPressed) moveVector += transform.forward;
        if (Keyboard.current.sKey.isPressed) moveVector -= transform.forward;
        if (Keyboard.current.dKey.isPressed) moveVector += transform.right;
        if (Keyboard.current.aKey.isPressed) moveVector -= transform.right;
        
        // Elevation controls: E to fly up, Q to drop down
        if (Keyboard.current.eKey.isPressed) moveVector += transform.up;
        if (Keyboard.current.qKey.isPressed) moveVector -= transform.up;

        // Apply clean directional translation normalized over time delta
        if (moveVector.sqrMagnitude > 0.01f)
        {
            transform.position += moveVector.normalized * activeSpeed * Time.deltaTime;
        }
    }
}