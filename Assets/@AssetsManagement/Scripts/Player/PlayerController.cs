using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Stats")]
    public PlayerStats stats = new PlayerStats();

    [Header("Look")]
    public Camera playerCamera;
    public float mouseSensitivity = 0.12f;
    public float minPitch = -85f;
    public float maxPitch = 85f;

    [Header("Physics")]
    public float gravity = -20f;

    [Header("Animation (optional, connect later)")]
    public Animator animator;
    public string lowerBodyOverrideLayerName = "LowerBodyOverride";
    public float gaspingModelTiltX = 14.7f;

    [Header("Hiding / Holding timing placeholders")]
    public float hideTransitionDuration = 1f;
    public float pickupAnimationDuration = 0.6f;

    [Header("Fear Screen Shake (while Fearing)")]
    public float fearShakePositionAmplitude = 0.02f;
    public float fearShakeRotationAmplitude = 0.6f;
    public float fearShakeSpeed = 18f;

    [Header("Third-Person View (Fearing / Gasping)")]
    public string playerBodyLayerName = "PlayerBody";
    public string playerLayerName = "Player";
    public Vector3 thirdPersonLocalPosition = new Vector3(0f, 2f, -2.8f);
    public float thirdPersonPivotHeight = 1.5f;
    public float thirdPersonCollisionRadius = 0.25f;
    public float thirdPersonCollisionBuffer = 0.25f;
    public float thirdPersonMinDistance = 0.4f;
    public float thirdPersonMinViewDistance = 1.2f;
    public float[] thirdPersonFallbackAngles = { 0f, 45f, -45f, 90f, -90f, 135f, -135f, 180f };

    public CharacterController Controller { get; private set; }
    public PlayerStateType CurrentStateType => currentState != null ? currentState.Type : PlayerStateType.Idle;
    public bool CameraLocked { get; set; }

    /// 미니게임 중처럼 조작을 전부 막아야 할 때. 시야만 잠그는 CameraLocked 와 달리
    /// 이동/앉기/던지기 입력 자체를 읽지 않는다. 퓨즈 각도를 A/D 로 맞추는 동안 몸이
    /// 같이 돌아가거나, 스킬체크의 S 키로 뒤로 걸어가는 문제를 막는다.
    public bool InputLocked { get; set; }
    public IHidingSpot ActiveHidingSpot { get; private set; }
    public IHoldable HeldItem { get; private set; }

    public Vector2 MoveAxis { get; private set; }
    public bool SitTogglePressed { get; private set; }
    public bool StopHeld { get; private set; }
    public bool InteractPressed { get; private set; }
    public bool ThrowPressed { get; private set; }
    public bool DropPressed { get; private set; }
    public bool SpacePressed { get; private set; }
    public bool HasMoveInput => MoveAxis.sqrMagnitude > 0.0001f;

    private IPlayerState currentState;
    private float pitch;
    private float verticalVelocity;

    [Header("Stationary Noise Override")]
    public float stationaryNoiseRange = 0.5f; // Stop 상태(WASD 입력 없음)에서 Idle/Walking/Sitting일 때 이 값으로 고정

    public PlayerActivityState Activity { get; private set; }
    private bool wantsSitting;

    [Header("Sitting Look")]
    public float sittingLookDownPitch = 10f; // Sitting 중 고개를 숙이는 정도(도)
    public float sittingEyeDropOffset = 0.73f; // Sitting 중 시야 높이를 이만큼 낮춤 (콜라이더 축소량과 동일 비율)
    public float sittingLookLerpSpeed = 6f;
    private float sittingLookOffset;
    private float sittingEyeOffset;
    private float carryLookOffset;   // 무거운 물건을 들었을 때 고개 숙임
    private float carryEyeOffset;    // 무거운 물건을 들었을 때 시점 하강

    [Header("Sitting Collider")]
    public float sittingColliderHeight = 1.05f; // 실제로 쪼그려 앉은 것처럼 Y축 판정만 줄임
    private float standingColliderHeight;
    private Vector3 standingColliderCenter;

    [Header("Vault")]
    public float vaultMaxRise = 1.30f;   // 발 기준 올라탈 수 있는 최대 높이
    public float vaultForward = 1.50f;   // 넘어간 뒤 앞으로 나아가는 거리
    public float vaultSeconds = 0.38f;
    private bool vaulting;
    // Pre-allocated: OverlapCapsuleNonAlloc would otherwise allocate an array every call.
    private readonly Collider[] headroom = new Collider[8];
    private Vector3 cameraBaseLocalPosition;
    private int playerBodyLayerMask;
    private int cameraBaseCullingMask;
    private int thirdPersonCollisionMask;
    private bool thirdPersonActive;
    private HashSet<string> animatorParamNames;
    private int lowerBodyOverrideLayer = -1;
    private bool lowerBodyLocked;
    private Transform[] lowerBodyBones;

    private Quaternion[] lowerBodyLocalRotations;
    private Vector3 hipsFrozenLocalPosition;
    private Quaternion modelBaseLocalRotation;

    private static readonly HumanBodyBones[] LowerBodyBoneIds =
    {
        HumanBodyBones.Hips,
        HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes,
        HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, HumanBodyBones.RightToes
    };

    public bool ThirdPersonActive => thirdPersonActive;

    [Header("Noise Range Gizmo (barely-visible ground disc)")]
    public float noiseRangeGizmoGroundOffset = 0.02f;
    public bool showNoiseRangeGizmo = true; // 하이어라키에서 이 컴포넌트 체크박스로 범위 표시 on/off
    private Transform noiseRangeDisc;

    private readonly IdleState idleState = new IdleState();
    private readonly WalkingState walkingState = new WalkingState();
    private readonly SittingState sittingState = new SittingState();
    private readonly StoppingState stoppingState = new StoppingState();
    private readonly GaspingState gaspingState = new GaspingState();
    private readonly FearingState fearingState = new FearingState();
    private readonly HidingState hidingState = new HidingState();
    private readonly HoldingState holdingState = new HoldingState();

    void Awake()
    {
        Controller = GetComponent<CharacterController>();
        standingColliderHeight = Controller.height;
        standingColliderCenter = Controller.center;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        playerBodyLayerMask = 1 << LayerMask.NameToLayer(playerBodyLayerName);
        int playerLayer = LayerMask.NameToLayer(playerLayerName);
        if (playerLayer >= 0) gameObject.layer = playerLayer;
        thirdPersonCollisionMask = ~(playerBodyLayerMask | (playerLayer >= 0 ? (1 << playerLayer) : 0));

        if (playerCamera != null)
        {
            cameraBaseLocalPosition = playerCamera.transform.localPosition;
            cameraBaseCullingMask = playerCamera.cullingMask & ~playerBodyLayerMask;
            playerCamera.cullingMask = cameraBaseCullingMask;
        }

        if (animator != null)
        {
            animator.applyRootMotion = false; // 이동은 전부 CharacterController가 담당, 애니메이션 자체 이동(루트 모션)은 무시
            modelBaseLocalRotation = animator.transform.localRotation;
            animatorParamNames = new HashSet<string>();
            foreach (var p in animator.parameters) animatorParamNames.Add(p.name);

            lowerBodyOverrideLayer = animator.GetLayerIndex(lowerBodyOverrideLayerName);
            if (lowerBodyOverrideLayer >= 0)
            {
                animator.SetLayerWeight(lowerBodyOverrideLayer, 0f);
                // 발이 바닥에 닿은 프레임(0)에 딱 한 번 고정해두고, 이후엔 가중치만 켜고 끈다 (재생 시점을 다시 건드리면 전환 블렌드로 잠깐 발이 뜬다)
                animator.Play("Idle", lowerBodyOverrideLayer, 0f);
            }

            lowerBodyBones = new Transform[LowerBodyBoneIds.Length];
            for (int i = 0; i < LowerBodyBoneIds.Length; i++)
            {
                lowerBodyBones[i] = animator.GetBoneTransform(LowerBodyBoneIds[i]);
            }
            lowerBodyLocalRotations = new Quaternion[LowerBodyBoneIds.Length];
        }

        noiseRangeDisc = RangeGizmo.CreateDisc(transform, "NoiseRange_Gizmo", new Color(0.2f, 0.5f, 1f, 0.05f));

        ChangeState(idleState);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        ReadInput();
        HandleCursorToggle();
        if (SpacePressed) TryVault();

        stats.TickFear(dt);
        if (currentState.Type != PlayerStateType.Stopping)
        {
            stats.RegenOxygen(dt);
        }

        // 전역 인터럽트: Fear가 최대치에 도달하면 어떤 상태든 즉시 Fearing으로 전환
        if (stats.IsFearMaxed && currentState.Type != PlayerStateType.Fearing)
        {
            ChangeState(fearingState);
        }

        if (!vaulting) currentState.Tick(this, dt);
        ApplyStationaryNoiseOverride();
        UpdateSittingLookOffset(dt);

        bool wantThirdPerson = currentState.Type == PlayerStateType.Fearing || currentState.Type == PlayerStateType.Gasping;
        UpdateCameraView(wantThirdPerson);

        if (!CameraLocked && !wantThirdPerson)
        {
            HandleLook();
        }

        HandleFearShake();

        if (noiseRangeDisc != null)
        {
            noiseRangeDisc.gameObject.SetActive(showNoiseRangeGizmo);
            if (showNoiseRangeGizmo) RangeGizmo.UpdateDisc(noiseRangeDisc, transform.position, stats.currentNoiseRange, noiseRangeGizmoGroundOffset);
        }
    }

    void UpdateSittingLookOffset(float dt)
    {
        bool sitting = currentState.Type == PlayerStateType.Sitting;
        sittingLookOffset = Mathf.Lerp(sittingLookOffset, sitting ? sittingLookDownPitch : 0f, dt * sittingLookLerpSpeed);
        sittingEyeOffset = Mathf.Lerp(sittingEyeOffset, sitting ? sittingEyeDropOffset : 0f, dt * sittingLookLerpSpeed);

        // 무거운 물건을 들면 고개가 숙여지고 시점이 내려간다. 앉기와 같은 방식으로 lerp 해서
        // 들었다 놓을 때 화면이 끊기지 않고 부드럽게 오간다.
        float targetLook = HeldItem != null ? HeldItem.CarryLookDownPitch : 0f;
        float targetEye = HeldItem != null ? HeldItem.CarryEyeDrop : 0f;
        carryLookOffset = Mathf.Lerp(carryLookOffset, targetLook, dt * sittingLookLerpSpeed);
        carryEyeOffset = Mathf.Lerp(carryEyeOffset, targetEye, dt * sittingLookLerpSpeed);
    }

    // pitch(마우스 시야)+sittingLookOffset 합이 90도를 넘으면 카메라 forward 벡터의 좌우 성분이 뒤집혀
    // 화면이 홱 돌아가 보이는 버그가 있었음 — 합산값을 안전 범위(minPitch~maxPitch)로 clamp.
    private float EffectivePitch => Mathf.Clamp(pitch + sittingLookOffset + carryLookOffset, minPitch, maxPitch);

    void UpdateCameraView(bool wantThirdPerson)
    {
        if (playerCamera == null) return;

        if (wantThirdPerson != thirdPersonActive)
        {
            thirdPersonActive = wantThirdPerson;
            playerCamera.cullingMask = thirdPersonActive
                ? (cameraBaseCullingMask | playerBodyLayerMask)
                : cameraBaseCullingMask;
        }

        if (thirdPersonActive)
        {
            Vector3 pivotWorldPos = transform.position + Vector3.up * thirdPersonPivotHeight;
            float horizDist = new Vector2(thirdPersonLocalPosition.x, thirdPersonLocalPosition.z).magnitude;
            float vertRise = thirdPersonLocalPosition.y - thirdPersonPivotHeight;

            float bestClearance = -1f;
            Vector3 bestDir = -transform.forward;
            float bestMaxDist = Mathf.Max(horizDist, 0.01f);

            foreach (float angle in thirdPersonFallbackAngles)
            {
                Vector3 horizDir = Quaternion.AngleAxis(angle, Vector3.up) * -transform.forward;
                Vector3 offset = horizDir * horizDist + Vector3.up * vertRise;
                float maxDist = offset.magnitude;
                Vector3 dir = maxDist > 0.0001f ? offset / maxDist : Vector3.up;

                float clearance = maxDist;
                RaycastHit hit;
                if (Physics.SphereCast(pivotWorldPos, thirdPersonCollisionRadius, dir, out hit, maxDist, thirdPersonCollisionMask, QueryTriggerInteraction.Ignore))
                {
                    clearance = hit.distance;
                }

                if (clearance > bestClearance)
                {
                    bestClearance = clearance;
                    bestDir = dir;
                    bestMaxDist = maxDist;
                }

                if (clearance >= thirdPersonMinViewDistance)
                {
                    break; // 뒤쪽부터 순서대로 시도해서, 여유가 충분한 첫 각도를 바로 채택
                }
            }

            float finalDist = bestClearance < bestMaxDist - 0.001f
                ? Mathf.Max(bestClearance - thirdPersonCollisionBuffer, thirdPersonMinDistance)
                : bestMaxDist;
            Vector3 finalWorldPos = pivotWorldPos + bestDir * finalDist;
            playerCamera.transform.position = finalWorldPos;
            playerCamera.transform.rotation = Quaternion.LookRotation((pivotWorldPos - finalWorldPos).normalized, Vector3.up);
        }
        else
        {
            Vector3 pos = cameraBaseLocalPosition;
            pos.y -= sittingEyeOffset + carryEyeOffset;
            playerCamera.transform.localPosition = pos;
            playerCamera.transform.localEulerAngles = new Vector3(EffectivePitch, 0f, 0f);
        }
    }

    void HandleFearShake()
    {
        if (playerCamera == null) return;

        if (currentState.Type == PlayerStateType.Fearing)
        {
            // UpdateCameraView가 이번 프레임의 기준 위치(1인칭 또는 3인칭)를 이미 세팅해뒀으니, 그 위에 흔들림만 더한다
            float t = Time.time * fearShakeSpeed;
            Vector3 posOffset = new Vector3(
                (Mathf.PerlinNoise(t, 0f) - 0.5f) * 2f,
                (Mathf.PerlinNoise(0f, t) - 0.5f) * 2f,
                0f) * fearShakePositionAmplitude;
            playerCamera.transform.localPosition += posOffset;

            float rotOffset = (Mathf.PerlinNoise(t, 5.5f) - 0.5f) * 2f * fearShakeRotationAmplitude;
            Vector3 e = playerCamera.transform.localEulerAngles;
            playerCamera.transform.localEulerAngles = new Vector3(e.x, e.y, rotOffset);
        }
        else
        {
            Vector3 e = playerCamera.transform.localEulerAngles;
            if (e.z != 0f)
            {
                playerCamera.transform.localEulerAngles = new Vector3(e.x, e.y, 0f);
            }
        }
    }

    void ReadInput()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null || InputLocked)
        {
            MoveAxis = Vector2.zero;
            SitTogglePressed = false;
            StopHeld = false;
            InteractPressed = false;
            DropPressed = false;
            SpacePressed = false;
            ThrowPressed = false;
            return;
        }

        float x = 0f, y = 0f;
        if (kb.wKey.isPressed) y += 1f;
        if (kb.sKey.isPressed) y -= 1f;
        if (kb.dKey.isPressed) x += 1f;
        if (kb.aKey.isPressed) x -= 1f;
        MoveAxis = Vector2.ClampMagnitude(new Vector2(x, y), 1f);

        SitTogglePressed = kb.leftCtrlKey.wasPressedThisFrame;
        StopHeld = kb.leftShiftKey.isPressed;
        InteractPressed = kb.fKey.wasPressedThisFrame;
        DropPressed = kb.gKey.wasPressedThisFrame;
        SpacePressed = kb.spaceKey.wasPressedThisFrame;

        ThrowPressed = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
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
            playerCamera.transform.localEulerAngles = new Vector3(EffectivePitch, 0f, 0f);
        }
    }

    public Vector3 MoveDirectionWorld()
    {
        Vector3 local = new Vector3(MoveAxis.x, 0f, MoveAxis.y);
        return transform.TransformDirection(local);
    }

    private void ApplyStationaryNoiseOverride()
    {
        Activity = HasMoveInput ? PlayerActivityState.Active : PlayerActivityState.Stop;

        bool stillState = currentState.Type == PlayerStateType.Idle
                        || currentState.Type == PlayerStateType.Walking
                        || currentState.Type == PlayerStateType.Sitting;
        if (!stillState) return;

        // 매 프레임 재계산 — Stop이면 0.5로 내리고, Active면 해당 상태의 정상값으로 복구.
        // (한쪽으로만 덮어쓰면 입력이 한 프레임만 끊겨도 0.5에 계속 고정되는 버그가 있었음)
        stats.currentNoiseRange = Activity == PlayerActivityState.Stop
            ? stationaryNoiseRange
            : stats.ActiveNoiseRangeFor(currentState.Type);
    }

    public void Move(Vector3 horizontalWorldVelocity)
    {
        if (Controller.isGrounded)
        {
            verticalVelocity = 0f;
        }
        verticalVelocity = Mathf.Max(verticalVelocity + gravity * Time.deltaTime, -30f); // 종단속도 clamp — 안 그러면 큰 dt 한 번에 무한히 떨어질 수 있음

        Vector3 full = horizontalWorldVelocity + Vector3.up * verticalVelocity;
        Controller.Move(full * Time.deltaTime);
    }

    public void SetAnimState(string name)
    {
        if (animator == null) return;
        if (animatorParamNames != null && !animatorParamNames.Contains(name)) return;
        animator.SetTrigger(name);
    }

    public void SetSittingCollider(bool active)
    {
        if (Controller == null) return;
        float height = active ? sittingColliderHeight : standingColliderHeight;
        Controller.height = height;
        Controller.center = new Vector3(standingColliderCenter.x, height * 0.5f, standingColliderCenter.z);
    }

    // BlockoutWalkPlayer.TryVault를 그대로 이식. 낮은 턱/창틀을 짚고 넘어가는 동작 (점프 아님).
    public bool TryVault()
    {
        // Controller.isGrounded는 이 프로젝트의 중력 처리 방식과 맞지 않아 항상 false로 나옴 -
        // 발밑 짧은 레이캐스트로 직접 확인한다.
        if (vaulting || !Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, 0.25f, ~0, QueryTriggerInteraction.Ignore))
            return false;
        Vector3 foot = transform.position;
        Vector3 fwd = transform.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.001f) return false;
        fwd.Normalize();

        // 1) 무릎에서 가슴까지 훑어 앞을 막는 턱을 찾는다.
        RaycastHit hit;
        bool found = false;
        Vector3 face = default;
        for (float h = 0.25f; h <= vaultMaxRise; h += 0.15f)
            if (Physics.Raycast(foot + Vector3.up * h, fwd, out hit, 1.0f, ~0, QueryTriggerInteraction.Ignore))
            { found = true; face = hit.point; break; }
        if (!found) return false;

        // 2) 턱 너머 위에서 아래로 쏴 윗면을 찾는다.
        Vector3 above = face + fwd * 0.30f + Vector3.up * (vaultMaxRise + 0.50f);
        if (!Physics.Raycast(above, Vector3.down, out hit, vaultMaxRise + 0.90f, ~0, QueryTriggerInteraction.Ignore))
            return false;
        float rise = hit.point.y - foot.y;
        if (rise < 0.15f || rise > vaultMaxRise) return false;

        // 3) 착지 자리에 웅크린 몸이 들어가는지. 안 들어가면 넘지 않는다.
        Vector3 dest = new Vector3(face.x, hit.point.y + 0.05f, face.z) + fwd * vaultForward;
        float r = Controller.radius - 0.02f;
        if (Physics.CheckCapsule(dest + Vector3.up * (r + 0.02f),
                                 dest + Vector3.up * (sittingColliderHeight - r - 0.02f), r, ~0, QueryTriggerInteraction.Ignore))
            return false;

        StartCoroutine(VaultTo(dest));
        return true;
    }

    private IEnumerator VaultTo(Vector3 dest)
    {
        vaulting = true;
        bool wasSitting = currentState.Type == PlayerStateType.Sitting;
        Controller.enabled = false; // 컨트롤러가 이동과 싸우지 않게
        SetSittingCollider(true);   // 창 개구부를 지나려면 판정을 낮춰야 한다
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
        Controller.enabled = true;
        vaulting = false;

        // 웅크림은 넘는 동안만이다. 원래 앉아있지 않았으면 도로 세운다. 착지 직후엔
        // 아직 창틀 밑이라 머리가 걸릴 수 있으므로, 설 자리가 날 때까지 잠깐 재시도한다.
        // 끝내 안 되면 웅크린 채로 두고 플레이어가 Ctrl로 편다.
        if (!wasSitting)
        {
            for (float t = 0f; t < 1.2f; t += Time.deltaTime)
            {
                if (CanStandUp())
                {
                    SetSittingCollider(false);
                    break;
                }
                yield return null;
            }
        }
    }

    private bool CanStandUp()
    {
        float radius = Controller.radius;
        Vector3 foot = transform.position + Vector3.up * (radius + 0.02f);
        Vector3 head = transform.position + Vector3.up * (standingColliderHeight - radius - 0.02f);
        int count = Physics.OverlapCapsuleNonAlloc(foot, head, radius - 0.02f, headroom,
            ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            // 스윕이 지금 서 있는 자리에서 이뤄지므로 자기 자신의 컨트롤러가 항상 잡힌다 - 그건 장애물이 아니다.
            if (headroom[i] != null && headroom[i] != Controller) return false;
        }
        return true;
    }

    public void SetGaspingModelTilt(bool active)
    {
        if (animator == null) return;
        animator.transform.localRotation = active
            ? modelBaseLocalRotation * Quaternion.Euler(gaspingModelTiltX, 0f, 0f)
            : modelBaseLocalRotation;
    }

    public void SetLowerBodyIdleOverride(bool active)
    {
        if (animator == null || lowerBodyOverrideLayer < 0) return;
        animator.SetLayerWeight(lowerBodyOverrideLayer, active ? 1f : 0f);

        if (active)
        {
            // Gasping 진입 직후 몇 프레임은 상체(Gasping)의 급격한 움직임 때문에
            // 마스크된 하체가 잠깐 들리는 과도현상이 있어, 여기서 미리 몇 프레임을 진행시켜
            // 값이 안정된 뒤의 포즈를 캡처해서 이후 매 프레임 그대로 고정시킨다.
            for (int i = 0; i < 4; i++) animator.Update(0.12f);

            for (int i = 0; i < lowerBodyBones.Length; i++)
            {
                if (lowerBodyBones[i] != null) lowerBodyLocalRotations[i] = lowerBodyBones[i].localRotation;
            }
            if (lowerBodyBones[0] != null) hipsFrozenLocalPosition = lowerBodyBones[0].localPosition;

            lowerBodyLocked = true;
        }
        else
        {
            lowerBodyLocked = false;
        }
    }

    void LateUpdate()
    {
        if (!lowerBodyLocked || lowerBodyBones == null) return;

        for (int i = 0; i < lowerBodyBones.Length; i++)
        {
            if (lowerBodyBones[i] == null) continue;
            lowerBodyBones[i].localRotation = lowerBodyLocalRotations[i];
        }
        if (lowerBodyBones[0] != null) lowerBodyBones[0].localPosition = hipsFrozenLocalPosition;
    }

    public void ChangeState(IPlayerState next)
    {
        currentState?.Exit(this);
        currentState = next;
        currentState.Enter(this);
    }

    public void GoToIdle() => ChangeState(idleState);
    public void GoToWalking() => ChangeState(walkingState);

    // Stopping/Hiding 등 인터럽트에서 돌아올 때 쓰는 진입점.
    // Ctrl로 명시적으로 일어나기 전까지(wantsSitting)는 Sitting을 그대로 유지한다.
    public void GoToIdleOrWalking() => ChangeState(ResolveIdleOrWalkingState());

    private IPlayerState ResolveIdleOrWalkingState()
    {
        if (wantsSitting) return sittingState;
        return HasMoveInput ? (IPlayerState)walkingState : idleState;
    }

    public void GoToSitting() { wantsSitting = true; ChangeState(sittingState); }
    public void ClearSittingIntent() => wantsSitting = false; // Ctrl로 실제 일어날 때만 호출
    public void GoToStopping() => ChangeState(stoppingState);
    public void EnterGasping() => ChangeState(gaspingState);

    // --- 미래의 Hiding/Holding 오브젝트를 위한 진입점 ---

    public void EnterHiding(IHidingSpot spot)
    {
        if (spot == null) return;
        ActiveHidingSpot = spot;
        ChangeState(hidingState);
    }

    public void ExitHidingInternal()
    {
        ActiveHidingSpot = null;
    }

    public void EnterHolding(IHoldable item)
    {
        if (item == null || HeldItem != null) return;
        HeldItem = item;
        item.OnPickedUp(this);
        ChangeState(holdingState);
    }

    public void ThrowHeldItem(Vector3 force)
    {
        if (HeldItem == null) return;
        HeldItem.OnThrown(force);
        HeldItem = null;
        GoToIdle();
    }

    public void DropHeldItem()
    {
        if (HeldItem == null) return;
        HeldItem.OnDropped();
        HeldItem = null;
        GoToIdle();
    }

    public void UseSedative()
    {
        stats.UseSedative();
    }

    void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
