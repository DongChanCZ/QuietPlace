using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public sealed class BlockoutWalkPlayer : MonoBehaviour
{
    public float walkSpeed = 3f;
    public float mouseSensitivity = 0.09f;

    CharacterController body;
    Camera eyes;
    Vector3 startPosition;
    Quaternion startRotation;
    float pitch;
    float verticalSpeed;

    void Awake()
    {
        body = GetComponent<CharacterController>();
        eyes = GetComponentInChildren<Camera>();
        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) SetCursor(false);
        else if (mouse != null && mouse.leftButton.wasPressedThisFrame) SetCursor(true);

        if (keyboard != null && keyboard.rKey.wasPressedThisFrame) ResetToStart();

        Vector2 movement = Vector2.zero;
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            if (mouse != null) ApplyLook(mouse.delta.ReadValue());
            if (keyboard != null)
                movement = new Vector2(
                    (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                    (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
        }
        Move(movement, Time.deltaTime);
    }

    public void ApplyLook(Vector2 mouseDelta)
    {
        transform.Rotate(0, mouseDelta.x * mouseSensitivity, 0);
        pitch = Mathf.Clamp(pitch - mouseDelta.y * mouseSensitivity, -80, 80);
        eyes.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
    }

    // The live controls and the stair regression check use this same movement path.
    public void Move(Vector2 input, float deltaTime)
    {
        if (deltaTime <= 0) return;
        input = Vector2.ClampMagnitude(input, 1);
        if (body.isGrounded && verticalSpeed < 0) verticalSpeed = -3f;
        verticalSpeed = Mathf.Max(verticalSpeed - 20f * deltaTime, -40f);
        Vector3 planar = transform.right * input.x + transform.forward * input.y;
        body.Move((planar * walkSpeed + Vector3.up * verticalSpeed) * deltaTime);
    }

    public void ResetToStart()
    {
        body.enabled = false;
        transform.SetPositionAndRotation(startPosition, startRotation);
        body.enabled = true;
        verticalSpeed = 0;
        pitch = 0;
        eyes.transform.localRotation = Quaternion.identity;
    }

    static void SetCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    void OnDisable() => SetCursor(false);
    void OnApplicationFocus(bool focused) { if (!focused) SetCursor(false); }

    void OnGUI()
    {
        GUI.Box(new Rect(12, 12, Screen.width - 24, 30),
            "이동 테스트 | 화면 클릭 후 WASD 이동 · 마우스 시점 · Esc 커서 · R 시작 위치");
        if (Cursor.lockState == CursorLockMode.Locked)
            GUI.Label(new Rect(Screen.width / 2f - 4, Screen.height / 2f - 10, 20, 20), "+");
    }
}
