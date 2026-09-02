using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerPreviewController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 4.5f;
    public float gravity = -20f;

    [Header("Look")]
    public float mouseSensitivity = 0.12f;
    public float minPitch = -85f;
    public float maxPitch = 85f;
    public Camera playerCamera;

    private CharacterController controller;
    private float pitch;
    private float verticalVelocity;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        HandleCursorToggle();
        HandleLook();
        HandleMove();
    }

    void HandleCursorToggle()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;
        if (kb.escapeKey.wasPressedThisFrame)
        {
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = locked;
        }
    }

    void HandleLook()
    {
        if (Mouse.current == null || Cursor.lockState != CursorLockMode.Locked) return;

        Vector2 delta = Mouse.current.delta.ReadValue();
        float yaw = delta.x * mouseSensitivity;
        pitch = Mathf.Clamp(pitch - delta.y * mouseSensitivity, minPitch, maxPitch);

        transform.Rotate(Vector3.up * yaw);
        if (playerCamera != null)
        {
            playerCamera.transform.localEulerAngles = new Vector3(pitch, 0f, 0f);
        }
    }

    void HandleMove()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        Vector3 input = Vector3.zero;
        if (kb.wKey.isPressed) input += Vector3.forward;
        if (kb.sKey.isPressed) input += Vector3.back;
        if (kb.aKey.isPressed) input += Vector3.left;
        if (kb.dKey.isPressed) input += Vector3.right;
        input = Vector3.ClampMagnitude(input, 1f);

        Vector3 move = transform.TransformDirection(input) * moveSpeed;

        if (controller.isGrounded)
        {
            verticalVelocity = 0f;
        }
        verticalVelocity += gravity * Time.deltaTime;

        Vector3 fullMove = move + Vector3.up * verticalVelocity;
        controller.Move(fullMove * Time.deltaTime);
    }

    void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
