using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class FacilityPlayerController : MonoBehaviour
{
    public float walkSpeed = 3.5f;
    public float runSpeed = 6f;
    public float mouseSensitivity = 0.09f;

    CharacterController controller;
    Camera viewCamera;
    Light playerFlashlight;
    float pitch;
    float verticalSpeed;
    bool hasKeycard;
    bool hasFlashlight;
    bool reachedLobby;
    bool inspectedLobbyMap;
    bool inspectedTornDocument;
    string objective = "책상에서 카드키와 손전등을 챙기세요";
    string notification = "";
    float notificationUntil;

    public bool HasKeycard => hasKeycard;
    public bool HasFlashlight => hasFlashlight;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        viewCamera = GetComponentInChildren<Camera>(true);
        CreatePlayerFlashlight();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        if (keyboard == null || mouse == null || viewCamera == null) return;

        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = locked;
        }
        if (mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        if (Cursor.lockState != CursorLockMode.Locked) return;

        Vector2 look = mouse.delta.ReadValue() * mouseSensitivity;
        transform.Rotate(0f, look.x, 0f);
        pitch = Mathf.Clamp(pitch - look.y, -82f, 82f);
        viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        float x = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
        float z = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
        Vector3 planar = transform.right * x + transform.forward * z;
        if (planar.sqrMagnitude > 1f) planar.Normalize();
        float speed = keyboard.leftShiftKey.isPressed ? runSpeed : walkSpeed;
        verticalSpeed = controller.isGrounded ? -1.5f : verticalSpeed + Physics.gravity.y * Time.deltaTime;
        controller.Move((planar * speed + Vector3.up * verticalSpeed) * Time.deltaTime);

        if (keyboard.eKey.wasPressedThisFrame) TryInteract();
        if (keyboard.fKey.wasPressedThisFrame && hasFlashlight && playerFlashlight != null)
            playerFlashlight.enabled = !playerFlashlight.enabled;
    }

    void CreatePlayerFlashlight()
    {
        if (viewCamera == null) return;
        foreach (Light legacyLight in viewCamera.GetComponents<Light>()) legacyLight.enabled = false;
        Transform existing = viewCamera.transform.Find("PLAYER_FLASHLIGHT");
        GameObject lightObject = existing != null ? existing.gameObject : new GameObject("PLAYER_FLASHLIGHT");
        lightObject.transform.SetParent(viewCamera.transform, false);
        lightObject.transform.localPosition = new Vector3(0.18f, -0.12f, 0.08f);
        lightObject.transform.localRotation = Quaternion.identity;
        playerFlashlight = lightObject.GetComponent<Light>();
        if (playerFlashlight == null) playerFlashlight = lightObject.AddComponent<Light>();
        playerFlashlight.type = LightType.Spot;
        playerFlashlight.color = new Color(0.78f, 0.88f, 1f);
        playerFlashlight.intensity = 2.2f;
        playerFlashlight.range = 16f;
        playerFlashlight.spotAngle = 47f;
        playerFlashlight.innerSpotAngle = 28f;
        playerFlashlight.shadows = LightShadows.Soft;
        playerFlashlight.enabled = false;
    }

    void TryInteract()
    {
        if (!Physics.SphereCast(viewCamera.transform.position, 0.08f, viewCamera.transform.forward,
            out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Collide)) return;
        FacilityPickup pickup = hit.collider.GetComponentInParent<FacilityPickup>();
        if (pickup != null) { pickup.Collect(this); return; }
        KeycardDoor door = hit.collider.GetComponentInParent<KeycardDoor>();
        if (door != null) { door.Interact(this); return; }
        FacilityClue clue = hit.collider.GetComponentInParent<FacilityClue>();
        if (clue != null) clue.Inspect(this);
    }

    public void CollectKeycard()
    {
        hasKeycard = true;
        ShowNotification("카드키를 획득했습니다.");
        UpdateObjective();
    }

    public void CollectFlashlight()
    {
        hasFlashlight = true;
        ShowNotification("손전등을 획득했습니다. F키로 켜고 끌 수 있습니다.");
        UpdateObjective();
    }

    void UpdateObjective()
    {
        if (hasKeycard && hasFlashlight) objective = "경비실을 나가 로비를 조사하세요";
        else if (hasKeycard) objective = "책상 위 손전등을 챙기세요";
        else if (hasFlashlight) objective = "책상 위 카드키를 챙기세요";
    }

    void ShowNotification(string text)
    {
        notification = text;
        notificationUntil = Time.time + 2.8f;
    }

    public void EnterLobby()
    {
        if (reachedLobby) return;
        reachedLobby = true;
        objective = hasKeycard && hasFlashlight ? "로비 안내도와 주변 단서를 조사하세요" : "경비실의 카드키와 손전등을 먼저 챙기세요";
        ShowNotification("로비에 진입했습니다.");
    }

    public void InspectLobbyClue(FacilityClue.ClueKind kind)
    {
        if (kind == FacilityClue.ClueKind.LobbyMap)
        {
            inspectedLobbyMap = true;
            objective = inspectedTornDocument ? "2층 기록보관실로 이동하세요" : "로비 반대편의 찢어진 문서를 조사하세요";
            ShowNotification("안내도에서 2층 기록보관실 위치를 확인했습니다.");
        }
        else
        {
            inspectedTornDocument = true;
            objective = inspectedLobbyMap ? "2층 기록보관실로 이동하세요" : "로비 안내도에서 기록보관실 위치를 확인하세요";
            ShowNotification("찢어진 문서에 2층 기록보관실이 표시되어 있습니다.");
        }
    }

    void OnGUI()
    {
        GUI.color = Color.white;
        GUI.Label(new Rect(18, 16, 650, 28), "WASD 이동 / Shift 달리기 / 마우스 시점 / Esc 커서");
        GUI.Label(new Rect(Screen.width / 2 - 5, Screen.height / 2 - 12, 20, 24), "+");
        GUI.Label(new Rect(18, 44, 520, 24), $"E 획득·조사 / F 손전등   카드키: {(hasKeycard ? "보유" : "없음")}   손전등: {(hasFlashlight ? "보유" : "없음")}");
        GUI.Box(new Rect(18, 72, 430, 30), $"목표: {objective}");
        if (Time.time < notificationUntil) GUI.Box(new Rect(Screen.width / 2 - 230, Screen.height - 105, 460, 38), notification);

        if (viewCamera != null && Physics.SphereCast(viewCamera.transform.position, 0.08f, viewCamera.transform.forward,
            out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Collide))
        {
            FacilityPickup pickup = hit.collider.GetComponentInParent<FacilityPickup>();
            if (pickup != null) GUI.Box(new Rect(Screen.width / 2 - 85, Screen.height / 2 + 28, 170, 30), $"E  {pickup.DisplayName} 획득");
            else
            {
                KeycardDoor door = hit.collider.GetComponentInParent<KeycardDoor>();
                if (door != null)
                {
                    GUI.Box(new Rect(Screen.width / 2 - 105, Screen.height / 2 + 28, 210, 30), $"E  {door.Prompt(this)}");
                    return;
                }
                FacilityClue clue = hit.collider.GetComponentInParent<FacilityClue>();
                if (clue != null) GUI.Box(new Rect(Screen.width / 2 - 85, Screen.height / 2 + 28, 170, 30), $"E  {clue.DisplayName} 조사");
            }
        }
    }
}
