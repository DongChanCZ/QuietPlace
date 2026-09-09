using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

/// Lets the real player trigger LabBlockout's Interactable objects (doors,
/// flashlight/keycard pickups) with E, the same way BlockoutWalkPlayer does
/// for the blockout walk-test. Kept separate from PlayerController so the
/// interaction sweep doesn't pile onto an already-large script.
public sealed class PlayerInteractor : MonoBehaviour, IInteractor
{
    public float interactionDistance = 3f;

    [Header("Battery")]
    public float batteryLevel = 100f;
    public float batteryDrainInterval = 3f; // 이 초마다 1씩 소모 (불 켜져 있을 때만)
    float batteryDrainTimer;

    Camera eyes;
    PlayerController playerController;
    Light playerFlashlight;
    bool hasFlashlight;
    bool hasKeycard;

    [Header("Held Item Models")]
    [SerializeField] GameObject flashlightModelPrefab;
    [SerializeField] GameObject brickModelPrefab;

    [Header("Throwing")]
    public float throwForce = 10f;
    public float throwUpwardArc = 2f;
    [SerializeField] GameObject landingRingPrefab;

    [Header("던진 물건이 떨어질 때 나는 소리")]
    [SerializeField] AudioClip bottleBreakSound;
    [SerializeField] AudioClip brickThrowSound;

    [Tooltip("떨어진 자리에서 울릴 3D 소스. 던진 물건은 파괴되므로 이쪽이 대신 울린다.")]
    [SerializeField] AudioSource throwImpactSource;

    [Tooltip("클립 앞에서 이만큼만 재생한다(초). 0 이면 끝까지.")]
    [SerializeField] float throwImpactSeconds = 1.5f;

    // 클립 이름은 코드에 둔다 - 인스펙터 참조는 병합 때 빠지지만 이 문자열은 남는다.
    const string BottleBreakName = "bottle_break";
    const string BrickThrowName = "brick_throw";

    Transform heldItemAnchor;
    GameObject brickHeldModel;
    GameObject bottleHeldModel;
    GameObject flashlightHeldModel;

    public bool HasFlashlight => hasFlashlight;
    public bool FlashlightEnabled => playerFlashlight != null && playerFlashlight.enabled;
    public bool HasKeycard => hasKeycard;
    public float BatteryLevel => batteryLevel;

    public event Action<string> OnNotice;

    void Awake()
    {
        eyes = GetComponentInChildren<Camera>();
        playerController = GetComponent<PlayerController>();
        CreatePlayerFlashlight();
        CreateHeldItemModels();

        // 인스펙터 참조는 병합 때 빠질 수 있다. 빠졌으면 이름으로 찾고 소스도 만든다.
        bottleBreakSound = AudioLibrary.Resolve(bottleBreakSound, BottleBreakName);
        brickThrowSound = AudioLibrary.Resolve(brickThrowSound, BrickThrowName);
        throwImpactSource = AudioLibrary.EnsureSource(throwImpactSource, transform, "ThrowImpactSource", 1f, 3f, 45f, 1f);
    }

    void OnEnable()
    {
        if (QuickSlotManager.Instance != null)
        {
            QuickSlotManager.Instance.OnSlotsChanged += HandleQuickSlotChanged;
            QuickSlotManager.Instance.OnItemAcquired += HandleItemAcquired;
        }
    }

    void OnDisable()
    {
        if (QuickSlotManager.Instance != null)
        {
            QuickSlotManager.Instance.OnSlotsChanged -= HandleQuickSlotChanged;
            QuickSlotManager.Instance.OnItemAcquired -= HandleItemAcquired;
        }
    }

    // 퀵슬롯에서 손전등이 선택 해제되면(다른 슬롯 선택/빈 슬롯 선택) 켜져 있던 불도 꺼야 한다.
    void HandleQuickSlotChanged()
    {
        if (!IsFlashlightSelected && playerFlashlight != null && playerFlashlight.enabled)
            playerFlashlight.enabled = false;
        RefreshHeldItemModel();
    }

    // 던질 수 있는(CanThrow) 아이템이 선택되어 있으면 카메라 앞에 실제 오브젝트로 보여준다.
    // UI가 아니라 진짜 월드 오브젝트라서, 나중에 던질 때 이 오브젝트를 그대로 부모에서 떼어내
    // 물리를 붙이면 된다(현재는 시각 표시까지만 - 던지기 자체는 별도 작업).
    void RefreshHeldItemModel()
    {
        QuickSlotManager manager = QuickSlotManager.Instance;
        ItemType item = default;
        bool hasSelection = manager != null && manager.TryGetSelectedItem(out item);
        bool canThrow = hasSelection && QuickSlotManager.CanThrow(item, batteryLevel);

        if (brickHeldModel != null) brickHeldModel.SetActive(canThrow && item == ItemType.Brick);
        if (bottleHeldModel != null) bottleHeldModel.SetActive(canThrow && item == ItemType.Bottle);
        if (flashlightHeldModel != null) flashlightHeldModel.SetActive(canThrow && item == ItemType.Flashlight);
    }

    void HandleItemAcquired(ItemType item, string displayName, int slotIndex)
    {
        int slotNumber = slotIndex + 1;
        string usageText = QuickSlotManager.IsUsable(item)
            ? slotNumber + "번을 눌러 선택 후 F를 눌러 사용 가능합니다."
            : slotNumber + "번을 눌러 선택할 수 있습니다.";
        ShowNotice(displayName + ObjectParticle(displayName) + " 습득하였습니다. " + usageText);
    }

    // 마지막 글자 받침 유무로 을/를 결정 (한글 음절 U+AC00~U+D7A3, 종성 인덱스 = (코드 - AC00) % 28, 0이면 받침 없음)
    static string ObjectParticle(string word)
    {
        if (string.IsNullOrEmpty(word)) return "를";
        char last = word[word.Length - 1];
        if (last < '가' || last > '힣') return "를";
        return (last - '가') % 28 != 0 ? "을" : "를";
    }

    bool IsFlashlightSelected =>
        QuickSlotManager.Instance != null &&
        QuickSlotManager.Instance.TryGetSelectedItem(out ItemType item) &&
        item == ItemType.Flashlight;

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.eKey.wasPressedThisFrame)
        {
            Interactable target = Aimed;
            if (target != null)
            {
                if (CanInteractWith(target)) target.Interact(this);
            }
            else if (playerController != null && playerController.HeldItem != null)
            {
                // 아무것도 겨누지 않은 상태에서 E 를 누르면 들고 있던 물건을 내려놓는다.
                // 슬롯이나 문을 보고 있을 때는 위쪽 분기가 먼저 처리하므로 설치가 우선된다.
                playerController.DropHeldItem();
            }
        }
        if (keyboard.fKey.wasPressedThisFrame) UseSelectedItem();
        // 숨기는 HidingSpotQTE(E + QTE)가 전담한다 - Q는 던지기 전용.
        if (keyboard.qKey.wasPressedThisFrame) ThrowSelectedItem();
        DrainBattery();
    }

    // 두 손으로 무거운 물건을 들고 있으면 새로 뭔가를 집을 수 없다.
    // 설치 슬롯이나 문처럼 손을 비우지 않아도 되는 것만 AllowedWhileHandsFull 로 통과시킨다.
    bool CanInteractWith(Interactable target)
    {
        bool handsFull = playerController != null && playerController.HeldItem != null;
        if (!handsFull || target.AllowedWhileHandsFull) return true;

        ShowNotice("양손이 차 있습니다. 먼저 내려놓으세요.");
        return false;
    }

    // F는 손전등이면 토글, 약이면 그 자리에서 소비 - 그 외(빈 슬롯/사용 불가 아이템)엔 아무 반응 없음.
    void UseSelectedItem()
    {
        if (QuickSlotManager.Instance == null || !QuickSlotManager.Instance.TryGetSelectedItem(out ItemType item)) return;
        if (item == ItemType.Flashlight) ToggleFlashlight();
        else if (item == ItemType.Medicine) UseMedicine();
    }

    void UseMedicine()
    {
        // 공포에 질려 이성적인 조작이 안 되는 상태 - 약을 꺼내 먹는 행동 자체가 불가능해야 한다.
        if (playerController != null && playerController.CurrentStateType == PlayerStateType.Fearing) return;
        if (playerController != null) playerController.stats.UseSedative();
        ShowNotice("\"조금 진정되는 것 같아\"");
        QuickSlotManager.Instance.RemoveItem(QuickSlotManager.Instance.SelectedIndex);
    }

    // Q: CanThrow 아이템이 선택되어 있으면 던진다. 실제 보유 아이템(퀵슬롯)은 사라지지만,
    // 손모델 원본은 heldItemAnchor에 그대로 남겨두고 복제본만 던진다 - 나중에 같은 종류를
    // 다시 주웠을 때 보여줄 손모델이 있어야 하기 때문.
    void ThrowSelectedItem()
    {
        QuickSlotManager manager = QuickSlotManager.Instance;
        if (manager == null || !manager.TryGetSelectedItem(out ItemType item)) return;
        if (!QuickSlotManager.CanThrow(item, batteryLevel)) return;

        GameObject source = HeldModelFor(item);
        if (source == null || eyes == null) return;

        GameObject thrown = Instantiate(source, source.transform.position, source.transform.rotation);
        thrown.name = source.name + "_Thrown";
        thrown.transform.SetParent(null, true);
        thrown.SetActive(true);
        AddThrowCollider(thrown, item);

        Rigidbody rb = thrown.AddComponent<Rigidbody>();
        rb.linearVelocity = eyes.transform.forward * throwForce + Vector3.up * throwUpwardArc;

        thrown.AddComponent<ThrownItem>().Init(landingRingPrefab, this, ImpactSoundFor(item));

        manager.RemoveItem(manager.SelectedIndex);
    }

    /// <summary>
    /// 던진 물건이 떨어진 자리에서 소리를 울린다. ThrownItem 이 부른다.
    ///
    /// 던진 물건은 닿는 순간 파괴되므로 그쪽에서 소리를 낼 수 없다. 여기서 3D 소스를
    /// 그 자리로 옮겨 재생하고, 클립이 길면(벽돌 소리는 14초다) 앞부분만 쓰고 끊는다.
    /// </summary>
    public void PlayThrowImpact(AudioClip clip, Vector3 at)
    {
        if (clip == null || throwImpactSource == null) return;

        throwImpactSource.transform.position = at;
        throwImpactSource.clip = clip;
        throwImpactSource.time = 0f;
        throwImpactSource.Play();

        if (throwImpactSeconds > 0f) StartCoroutine(StopThrowImpactAfter(clip, throwImpactSeconds));
    }

    System.Collections.IEnumerator StopThrowImpactAfter(AudioClip clip, float seconds)
    {
        yield return new WaitForSeconds(seconds);

        // 그 사이에 다른 물건을 던져 다른 클립이 올라갔으면 건드리지 않는다.
        if (throwImpactSource != null && throwImpactSource.clip == clip) throwImpactSource.Stop();
    }

    /// 던진 물건이 떨어질 때 나는 소리. 물건마다 다르다.
    AudioClip ImpactSoundFor(ItemType item)
    {
        if (item == ItemType.Bottle) return bottleBreakSound;
        if (item == ItemType.Brick) return brickThrowSound;
        return null;   // 손전등 등은 아직 전용 소리가 없다
    }

    GameObject HeldModelFor(ItemType item)
    {
        if (item == ItemType.Brick) return brickHeldModel;
        if (item == ItemType.Bottle) return bottleHeldModel;
        if (item == ItemType.Flashlight) return flashlightHeldModel;
        return null;
    }

    // 손모델(임시 도형)은 손에 든 동안 콜라이더를 빼뒀으니(DisableShadows 근처 참고),
    // 던지는 복제본에는 물리 충돌을 위해 다시 붙여준다. 형태별 근사치라 정밀하진 않음.
    static void AddThrowCollider(GameObject go, ItemType item)
    {
        switch (item)
        {
            case ItemType.Brick:
                go.AddComponent<BoxCollider>();
                break;
            case ItemType.Bottle:
                go.AddComponent<SphereCollider>();
                break;
            default: // Flashlight
                CapsuleCollider capsule = go.AddComponent<CapsuleCollider>();
                capsule.direction = 2; // Z축이 긴 방향
                capsule.radius = 0.08f;
                capsule.height = 0.4f;
                break;
        }
    }

    void DrainBattery()
    {
        if (playerFlashlight == null || !playerFlashlight.enabled) return;
        if (batteryLevel <= 0f) { playerFlashlight.enabled = false; return; }
        batteryDrainTimer += Time.deltaTime;
        if (batteryDrainTimer < batteryDrainInterval) return;
        batteryDrainTimer -= batteryDrainInterval;
        batteryLevel = Mathf.Max(0f, batteryLevel - 1f);
        if (batteryLevel <= 0f)
        {
            playerFlashlight.enabled = false;
            // 배터리가 다 되면 손에서 놓친 것처럼 선택도 풀린다 - 다시 숫자를 눌러야 재선택되고,
            // 그때는 CanThrow가 true라 던질 수 있는 물건으로 취급된다.
            QuickSlotManager.Instance?.ClearSelection();
        }
    }

    // 위치는 BlockoutWalkPlayer와 동일(블록아웃 인스턴스의 (1.5, 1.3, 1.5) 스케일 배율 기준),
    // 방향/각도는 크로스헤어 수렴 보정 때문에 이후 별도로 조정됨(아래 CreatePlayerFlashlight 참고).
    void CreatePlayerFlashlight()
    {
        if (eyes == null) return;
        GameObject lightObject = new GameObject("PLAYER_FLASHLIGHT");
        lightObject.transform.SetParent(eyes.transform, false);
        Vector3 localPos = new Vector3(0.16f, -0.12f, 0.08f);
        lightObject.transform.localPosition = localPos;
        // 카메라와 평행하게 두면 손전등이 카메라 옆에 오프셋되어 있는 만큼 크로스헤어와 어긋나 보인다.
        // 정면 10m 지점(카메라 축 위의 한 점)을 바라보게 해서 그 근방 거리에서 크로스헤어와 수렴하게 한다.
        lightObject.transform.localRotation = Quaternion.LookRotation(new Vector3(0f, 0f, 10f) - localPos);
        playerFlashlight = lightObject.AddComponent<Light>();
        playerFlashlight.type = LightType.Spot;
        playerFlashlight.color = new Color(0.78f, 0.88f, 1f);
        playerFlashlight.intensity = 25f;
        playerFlashlight.range = 26f;
        playerFlashlight.spotAngle = 57.6f; // 48 * 1.2 - 반경 20% 확장
        playerFlashlight.innerSpotAngle = 33.6f; // 28 * 1.2 - 같은 비율 유지
        playerFlashlight.shadows = LightShadows.Soft;
        playerFlashlight.shadowStrength = 0.85f;
        playerFlashlight.enabled = false;
    }

    // ponytail: 벽돌/유리병 대신 임시 도형(Cube/Sphere)을 카메라 자식으로 붙여서 "손에 든" 위치에 둔다.
    // 실제 모델 들어오면 이 두 오브젝트만 교체하면 됨 - 앵커/토글 구조는 그대로.
    void CreateHeldItemModels()
    {
        if (eyes == null) return;
        GameObject anchorGO = new GameObject("HeldItemAnchor");
        anchorGO.transform.SetParent(eyes.transform, false);
        anchorGO.transform.localPosition = new Vector3(0.32f, -0.28f, 0.55f);
        anchorGO.transform.localRotation = Quaternion.Euler(-10f, -25f, 0f);
        heldItemAnchor = anchorGO.transform;

        if (brickModelPrefab != null)
        {
            brickHeldModel = Instantiate(brickModelPrefab, heldItemAnchor);
            brickHeldModel.name = "HeldBrick";
            brickHeldModel.transform.localPosition = Vector3.zero;
            // localRotation/localScale은 안 건드림 - 벽돌의 각도/비율 자체가 프리팹에 저장돼 있음
            foreach (Collider col in brickHeldModel.GetComponentsInChildren<Collider>())
                Destroy(col);
            DisableShadows(brickHeldModel);
            brickHeldModel.SetActive(false);
        }

        bottleHeldModel = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bottleHeldModel.name = "HeldBottle";
        bottleHeldModel.transform.SetParent(heldItemAnchor, false);
        bottleHeldModel.transform.localScale = Vector3.one * 0.16f;
        Destroy(bottleHeldModel.GetComponent<Collider>());
        DisableShadows(bottleHeldModel);
        bottleHeldModel.SetActive(false);

        if (flashlightModelPrefab != null)
        {
            flashlightHeldModel = Instantiate(flashlightModelPrefab, heldItemAnchor);
            flashlightHeldModel.name = "HeldFlashlight";
            flashlightHeldModel.transform.localPosition = Vector3.zero;
            flashlightHeldModel.transform.localRotation = Quaternion.identity;
            flashlightHeldModel.transform.localScale = Vector3.one;
            foreach (Collider col in flashlightHeldModel.GetComponentsInChildren<Collider>())
                Destroy(col);
            DisableShadows(flashlightHeldModel);
            flashlightHeldModel.SetActive(false);
        }
    }

    // 카메라 바로 앞이라 그림자를 드리우면 화면 구석에 어색한 그림자가 생긴다 - 손에 든 모델은 그림자 없이.
    static void DisableShadows(GameObject go)
    {
        foreach (MeshRenderer renderer in go.GetComponentsInChildren<MeshRenderer>())
            renderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    public void ToggleFlashlight()
    {
        if (!hasFlashlight || playerFlashlight == null || !IsFlashlightSelected || batteryLevel <= 0f) return;
        playerFlashlight.enabled = !playerFlashlight.enabled;
    }

    readonly RaycastHit[] reach = new RaycastHit[16];

    int aimedFrame = -1;
    Interactable aimed;

    /// 조준 중인 Interactable. 프레임당 한 번만 스윕한다 - Update(E키)와 프롬프트 UI가
    /// 같은 프레임에 둘 다 물어봐도 레이캐스트는 한 번만 돈다.
    public Interactable Aimed
    {
        get
        {
            if (aimedFrame != Time.frameCount)
            {
                aimedFrame = Time.frameCount;
                aimed = AimedInteractable();
            }
            return aimed;
        }
    }

    readonly System.Collections.Generic.List<Interactable> interactableBuffer = new System.Collections.Generic.List<Interactable>(4);

    /// 맞은 콜라이더에서 위로 올라가며 '켜져 있는' Interactable 을 찾는다.
    ///
    /// GetComponentInParent 는 enabled 를 보지 않고 첫 번째 것만 돌려준다. 은신 캐비닛처럼
    /// 한 오브젝트에 Interactable 이 둘(꺼둔 SlidingDoor + HidingSpot)이면 꺼진 쪽이 잡혀
    /// 조준이 통째로 실패했다. 형제까지 훑어 켜진 것을 고른다.
    Interactable FindEnabledInteractable(Transform from)
    {
        for (Transform t = from; t != null; t = t.parent)
        {
            t.GetComponents(interactableBuffer);
            for (int i = 0; i < interactableBuffer.Count; i++)
                if (interactableBuffer[i].enabled) return interactableBuffer[i];
        }
        return null;
    }

    Interactable AimedInteractable()
    {
        if (eyes == null) return null;
        int count = Physics.SphereCastNonAlloc(eyes.transform.position, 0.08f,
            eyes.transform.forward, reach, interactionDistance, ~0, QueryTriggerInteraction.Collide);
        Interactable nearest = null;
        Collider nearestCollider = null;
        float nearestDistance = float.PositiveInfinity;
        int bestPriority = int.MinValue;
        for (int i = 0; i < count; i++)
        {
            var candidate = FindEnabledInteractable(reach[i].collider.transform);
            if (candidate == null) continue;

            // 지금 쓸 수 없는 것은 조준 대상이 아니다. 이게 없으면 손에 든 물건이나
            // 잠긴 판 안쪽의 부품이 우선순위로 조준을 차지한 채 아무 안내도 내지 않아,
            // 뒤에 있는 진짜 대상(설치 슬롯, 고정판)을 가려버린다.
            if (!candidate.Available(this)) continue;

            // 우선순위가 먼저, 같으면 가까운 쪽. 거리만 보면 큰 기계 콜라이더에
            // 파묻힌 작은 물건(스패너, 퓨즈)을 영영 집을 수 없다.
            int priority = candidate.AimPriority;
            if (priority < bestPriority) continue;
            if (priority == bestPriority && reach[i].distance >= nearestDistance) continue;

            nearest = candidate;
            nearestCollider = reach[i].collider;
            nearestDistance = reach[i].distance;
            bestPriority = priority;
        }
        if (nearest == null) return null;

        // 벽 너머의 물건을 집던 버그를 막는다. 위 스피어캐스트는 조준을 넉넉하게 잡으려고
        // 반경이 있어서, 그 히트로 가림 판정을 하면 책상 모서리나 선반 판처럼 물건이
        // 놓여 있는 면까지 '가림'으로 세어 아무것도 못 집게 된다.
        // 가림 판정만 굵기 없는 선으로 따로 본다.
        Vector3 from = eyes.transform.position;
        Vector3 to = nearestCollider.ClosestPoint(from);
        RaycastHit block;
        if (Physics.Linecast(from, to, out block, ~0, QueryTriggerInteraction.Ignore)
            && FindEnabledInteractable(block.collider.transform) != nearest
            && !block.collider.transform.IsChildOf(transform)
            // 물건이 그 콜라이더 '안에' 있으면 가림막일 수 없다. 책상처럼 콜라이더가
            // 통짜 상자인 가구는 위에 올려둔 물건까지 부피 안에 품어버려서,
            // 이걸 빼지 않으면 책상 앞에 서도 그 위의 손전등을 집을 수 없다.
            && !block.collider.bounds.Contains(to))
            return null;

        return nearest;
    }

    public void GiveFlashlight()
    {
        hasFlashlight = true;
        QuickSlotManager.Instance?.AddItem(ItemType.Flashlight, "손전등");
    }

    public void GiveKeycard() => hasKeycard = true;
    public void ShowNotice(string text) => OnNotice?.Invoke(text);
}
