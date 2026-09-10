using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public sealed class BlockoutWalkPlayer : MonoBehaviour, IInteractor
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
    bool noticePersistent;

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
                if (keyboard.spaceKey.wasPressedThisFrame) TryVault();
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
        noticePersistent = false;
    }

    public void ShowPersistentNotice(string text)
    {
        notice = text;
        noticePersistent = true;
    }

    public void ClearPersistentNotice()
    {
        noticePersistent = false;
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
        // 수평 이동을 먼저, 중력을 나중에 따로 넣는다. 한 번에 합쳐서 넣으면
        // 아래로 향한 성분이 CharacterController 의 계단 오르기(stepOffset)를
        // 막아 0.2m 턱도 못 넘는다. 경사로만 있을 때는 드러나지 않던 문제다.
        body.Move(planar * speed * deltaTime);
        body.Move(Vector3.up * verticalSpeed * deltaTime);
    }

    // 넘어가기. 창턱이나 낮은 턱을 짚고 넘는 동작이다. 걸어 올라갈 수 없는
    // 높이(stepOffset 0.30 초과)를 이걸로 통과한다.
    public float vaultMaxRise = 1.30f;      // 발 기준 올라탈 수 있는 최대 높이
    public float vaultForward = 0.60f;      // 넘어간 뒤 앞으로 나아가는 거리
    public float vaultSeconds = 0.38f;
    bool vaulting;

    public bool IsVaulting => vaulting;

    /// 앞의 턱을 찾아 넘어간다. 넘을 곳이 없거나 몸이 안 들어가면 아무 일도 없다.
    public bool TryVault() { return TryVault(false); }

    /// instant 는 검증용이다. 볼트 모션은 코루틴이라 실제 프레임이 필요한데,
    /// 체크는 Move 를 손으로 돌리므로 코루틴이 진행되지 않는다.
    public bool TryVault(bool instant)
    {
        if (vaulting || !body.isGrounded) return false;
        Vector3 foot = transform.position;
        Vector3 fwd = transform.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.001f) return false;
        fwd.Normalize();

        // 1) 무릎에서 가슴까지 훑어 앞을 막는 턱을 찾는다.
        RaycastHit hit;
        bool found = false;
        Vector3 face = default;
        // 0.25 에서 시작하면 쓰레기통 뚜껑(1.30) 위에서 창틀(윗면 1.55)을 1cm 차이로
        // 지나쳐 창문이 안 잡힌다. 무릎보다 낮게 시작한다.
        for (float h = 0.12f; h <= vaultMaxRise; h += 0.12f)
            if (Physics.Raycast(foot + Vector3.up * h, fwd, out hit, 1.0f, ~0, QueryTriggerInteraction.Ignore))
            { found = true; face = hit.point; break; }
        if (!found) return false;

        // 2) 턱 너머 위에서 아래로 쏴 윗면을 찾는다.
        //    한 지점만 보면 안 된다 - 창틀 깊이가 0.33 뿐이라 앞으로 0.30 을 고정으로
        //    밀면 앞 검사가 어느 부재를 맞았느냐에 따라 5cm 차이로 창틀을 빗나간다.
        float topY = 0f;
        bool top = false;
        foreach (float step in new float[] { 0.12f, 0.20f, 0.30f, 0.42f })
        {
            Vector3 above = face + fwd * step + Vector3.up * (vaultMaxRise + 0.50f);
            // 창문은 위에 상부틀이 있어서 첫 히트가 늘 그것이다. 짚고 넘을 하부틀은
            // 그 아래에 있으므로 전부 받아서 올라탈 수 있는 높이 중 가장 높은 면을 고른다.
            RaycastHit[] downs = Physics.RaycastAll(above, Vector3.down, vaultMaxRise + 0.90f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < downs.Length; i++)
            {
                float rise = downs[i].point.y - foot.y;
                if (rise < 0.15f || rise > vaultMaxRise) continue;
                if (top && downs[i].point.y <= topY) continue;
                topY = downs[i].point.y;
                top = true;
            }
            if (top) break;
        }
        if (!top) return false;

        // 3) 착지 자리에 웅크린 몸이 들어가는지. 안 들어가면 넘지 않는다 -
        //    벽 속으로 밀어 넣는 것보다 아무 일도 안 일어나는 편이 낫다.
        Vector3 dest = new Vector3(face.x, topY + 0.05f, face.z) + fwd * vaultForward;
        float r = body.radius - 0.02f;
        if (Physics.CheckCapsule(dest + Vector3.up * (r + 0.02f),
                                 dest + Vector3.up * (CrouchHeight - r - 0.02f), r, ~0, QueryTriggerInteraction.Ignore))
            return false;

        // 4) 턱 위가 트여 있어야 한다. 넘기는 컨트롤러를 끄고 트랜스폼을 직접 옮기므로,
        //    이 검사가 없으면 통짜 벽 앞에서도 넘기가 걸려 벽을 뚫고 나간다.
        //    창틀·쓰레기통처럼 위가 열린 것만 통과한다.
        Vector3 overFrom = new Vector3(face.x, topY + 0.25f, face.z) - fwd * 0.25f;
        Vector3 overTo = new Vector3(dest.x, topY + 0.25f, dest.z);
        if (Physics.Linecast(overFrom, overTo, ~0, QueryTriggerInteraction.Ignore))
            return false;

        if (instant)
        {
            bool wasCrouching = crouching;
            body.enabled = false;
            transform.position = dest;
            body.enabled = true;
            verticalSpeed = 0f;
            // 넘는 동안에만 웅크린다. 서 있었으면 도로 세운다.
            if (!wasCrouching) SetCrouch(false);
        }
        else StartCoroutine(VaultTo(dest));
        return true;
    }

    IEnumerator VaultTo(Vector3 dest)
    {
        vaulting = true;
        bool wasCrouching = crouching;
        body.enabled = false;                 // 컨트롤러가 이동과 싸우지 않게
        SetCrouch(true);                      // 창 개구부를 지나려면 낮춰야 한다
        Vector3 from = transform.position;
        float peak = Mathf.Max(from.y, dest.y) + 0.25f;
        for (float t = 0f; t < vaultSeconds; t += Time.deltaTime)
        {
            float k = Mathf.Clamp01(t / vaultSeconds);
            // 손을 짚고 올라타는 느낌: 먼저 뜨고 나중에 앞으로 넘어간다.
            Vector3 p = Vector3.Lerp(from, dest, k * k * (3f - 2f * k));
            p.y = Mathf.Lerp(Mathf.Lerp(from.y, peak, Mathf.Clamp01(k / 0.5f)),
                             dest.y, Mathf.Clamp01((k - 0.5f) / 0.5f));
            transform.position = p;
            yield return null;
        }
        transform.position = dest;
        body.enabled = true;
        verticalSpeed = 0f;
        vaulting = false;

        // 웅크림은 넘는 동안만이다. 원래 서 있었으면 도로 세운다. 착지 직후에는
        // 아직 창틀 밑이라 머리가 걸릴 수 있으므로, 설 자리가 날 때까지 잠깐
        // 다시 시도한다. 끝내 안 되면 웅크린 채로 두고 플레이어가 C 로 편다.
        if (!wasCrouching)
            for (float t = 0f; t < 1.2f && crouching; t += Time.deltaTime)
            {
                SetCrouch(false);
                if (!crouching) break;
                yield return null;
            }
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
            "화면 클릭 후 WASD 이동 · 마우스 시점 · Space 짚고 올라가기 · C 웅크리기(토글) · E 획득 · F 손전등 · Esc 커서 · R 시작 위치");
        GUI.Label(new Rect(18, 46, 460, 24),
            $"손전등: {(hasFlashlight ? "보유" : "없음")} · 빛: {(FlashlightEnabled ? "켜짐" : "꺼짐")} · 카드키: {(hasKeycard ? "보유" : "없음")} · 자세: {(crouching ? "웅크림" : "서 있음")}");
        if (Cursor.lockState == CursorLockMode.Locked)
            GUI.Label(new Rect(Screen.width / 2f - 4, Screen.height / 2f - 10, 20, 20), "+");
        if (Time.time < noticeUntil || noticePersistent)
            GUI.Box(new Rect(Screen.width / 2f - 230, Screen.height - 92, 460, 36), notice);
        Interactable target = AimedInteractable();
        string prompt = target != null ? target.Prompt(this) : null;
        if (!string.IsNullOrEmpty(prompt))
            GUI.Box(new Rect(Screen.width / 2f - 140, Screen.height / 2f + 28, 280, 30), prompt);
    }
}
