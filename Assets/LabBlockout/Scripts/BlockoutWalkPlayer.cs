using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public sealed class BlockoutWalkPlayer : MonoBehaviour
{
    public float walkSpeed = 3f;
    public float mouseSensitivity = 0.09f;
    public float interactionDistance = 3f;

    // The guard-room window opening is 1.56 m tall and the standing capsule is
    // 1.80 m, so climbing in needs a crouch, not a taller ramp.
    const float StandHeight = 1.80f;
    const float CrouchHeight = 1.05f;
    const float StandEye = 1.65f;
    const float CrouchEye = 0.92f;
    public float crouchSpeed = 1.4f;

    CharacterController body;
    Camera eyes;
    Light playerFlashlight;
    Vector3 startPosition;
    Quaternion startRotation;
    float pitch;
    float verticalSpeed;
    bool hasFlashlight;
    bool hasKeycard;
    bool crouching;
    string notice = "";
    float noticeUntil;

    public bool IsCrouching => crouching;

    public bool HasFlashlight => hasFlashlight;
    public bool FlashlightEnabled => playerFlashlight != null && playerFlashlight.enabled;

    void Awake()
    {
        body = GetComponent<CharacterController>();
        eyes = GetComponentInChildren<Camera>();
        startPosition = transform.position;
        startRotation = transform.rotation;
        CreatePlayerFlashlight();
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
            {
                if (keyboard.eKey.wasPressedThisFrame) TryInteract();
                if (keyboard.fKey.wasPressedThisFrame) ToggleFlashlight();
                // Toggle, not hold: you crouch to get somewhere and want both
                // hands free once you are there.
                if (keyboard.cKey.wasPressedThisFrame || keyboard.leftCtrlKey.wasPressedThisFrame)
                    SetCrouch(!crouching);
                movement = new Vector2(
                    (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                    (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
            }
        }
        Move(movement, Time.deltaTime);
    }

    // Public so the play-mode checks can crouch the player without faking input -
    // the Input System will not deliver held keys while the editor is unfocused.
    public void SetCrouch(bool wanted)
    {
        if (wanted == crouching) return;
        // Standing up inside a window reveal or under a duct would push the
        // capsule through the ceiling, so refuse while something is overhead.
        if (!wanted && !CanStandUp()) return;
        crouching = wanted;
        float height = crouching ? CrouchHeight : StandHeight;
        body.height = height;
        body.center = new Vector3(0f, height * 0.5f, 0f);
        if (eyes != null)
        {
            Vector3 eye = eyes.transform.localPosition;
            eye.y = crouching ? CrouchEye : StandEye;
            eyes.transform.localPosition = eye;
        }
    }

    // Pre-allocated: this runs from Update and OverlapCapsule would otherwise
    // allocate an array every frame.
    readonly Collider[] headroom = new Collider[8];

    bool CanStandUp()
    {
        Vector3 foot = transform.position + Vector3.up * (body.radius + 0.02f);
        Vector3 head = transform.position + Vector3.up * (StandHeight - body.radius - 0.02f);
        int count = Physics.OverlapCapsuleNonAlloc(foot, head, body.radius - 0.02f, headroom,
            ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            // The sweep sits where the player already is, so it always finds the
            // player's own controller. Counting that as an obstruction is what
            // made standing up impossible once you had crouched.
            if (headroom[i] != null && headroom[i] != body) return false;
        }
        return true;
    }

    void CreatePlayerFlashlight()
    {
        if (eyes == null) return;
        Transform existing = eyes.transform.Find("PLAYER_FLASHLIGHT");
        GameObject lightObject = existing != null ? existing.gameObject : new GameObject("PLAYER_FLASHLIGHT");
        lightObject.transform.SetParent(eyes.transform, false);
        lightObject.transform.localPosition = new Vector3(0.16f, -0.12f, 0.08f);
        lightObject.transform.localRotation = Quaternion.identity;
        playerFlashlight = lightObject.GetComponent<Light>();
        if (playerFlashlight == null) playerFlashlight = lightObject.AddComponent<Light>();
        playerFlashlight.type = LightType.Spot;
        playerFlashlight.color = new Color(0.78f, 0.88f, 1f);
        // Tuned against the scene, not on paper. The blockout instance carries a
        // (1.5, 1.3, 1.5) override, so every distance is half again as long as
        // the design metres say and light falls off with the square of that -
        // the old 2.4 lit nothing past arm's reach once the room went dark.
        playerFlashlight.intensity = 25f;
        playerFlashlight.range = 26f;
        playerFlashlight.spotAngle = 48f;
        playerFlashlight.innerSpotAngle = 28f;
        // The flashlight is the only light the player carries through a dark
        // building, so its shadows are most of what the lighting pass buys.
        playerFlashlight.shadows = LightShadows.Soft;
        playerFlashlight.shadowStrength = 0.85f;
        playerFlashlight.enabled = false;
    }

    // Pre-allocated: this is swept every frame for the prompt, and SphereCastAll
    // would allocate a fresh array each time.
    readonly RaycastHit[] reach = new RaycastHit[16];
    Interactable aimed;
    int aimedFrame = -1;

    void TryInteract()
    {
        Interactable target = AimedInteractable();
        if (target != null) target.Interact(this);
    }

    /// The thing under the crosshair, resolved at most once per frame. Update
    /// and the HUD both ask for it, and it used to be swept twice.
    Interactable AimedInteractable()
    {
        if (aimedFrame == Time.frameCount) return aimed;
        aimedFrame = Time.frameCount;
        aimed = null;
        if (eyes == null) return null;
        int count = Physics.SphereCastNonAlloc(eyes.transform.position, 0.08f,
            eyes.transform.forward, reach, interactionDistance, ~0, QueryTriggerInteraction.Collide);
        float nearest = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            var candidate = reach[i].collider.GetComponentInParent<Interactable>();
            if (candidate == null || reach[i].distance >= nearest) continue;
            aimed = candidate;
            nearest = reach[i].distance;
        }
        return aimed;
    }

    public void GiveFlashlight()
    {
        if (hasFlashlight) return;
        hasFlashlight = true;
        ShowNotice("손전등을 획득했습니다. F키로 켜고 끌 수 있습니다.");
    }

    public bool HasKeycard => hasKeycard;

    public void GiveKeycard()
    {
        if (hasKeycard) return;
        hasKeycard = true;
        ShowNotice("카드키를 획득했습니다. 잠긴 문을 열 수 있습니다.");
    }

    public void ShowNotice(string text)
    {
        notice = text;
        noticeUntil = Time.time + 2.8f;
    }

    public void ToggleFlashlight()
    {
        if (!hasFlashlight || playerFlashlight == null) return;
        playerFlashlight.enabled = !playerFlashlight.enabled;
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
        float speed = crouching ? crouchSpeed : walkSpeed;
        body.Move((planar * speed + Vector3.up * verticalSpeed) * deltaTime);
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
            "화면 클릭 후 WASD 이동 · 마우스 시점 · C 웅크리기(토글) · E 획득 · F 손전등 · Esc 커서 · R 시작 위치");
        GUI.Label(new Rect(18, 46, 460, 24),
            $"손전등: {(hasFlashlight ? "보유" : "없음")} · 빛: {(FlashlightEnabled ? "켜짐" : "꺼짐")} · 카드키: {(hasKeycard ? "보유" : "없음")} · 자세: {(crouching ? "웅크림" : "서 있음")}");
        if (Cursor.lockState == CursorLockMode.Locked)
            GUI.Label(new Rect(Screen.width / 2f - 4, Screen.height / 2f - 10, 20, 20), "+");
        if (Time.time < noticeUntil)
            GUI.Box(new Rect(Screen.width / 2f - 230, Screen.height - 92, 460, 36), notice);
        Interactable target = AimedInteractable();
        string prompt = target != null ? target.Prompt(this) : null;
        if (!string.IsNullOrEmpty(prompt))
            GUI.Box(new Rect(Screen.width / 2f - 140, Screen.height / 2f + 28, 280, 30), prompt);
    }
}
