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

    [Header("Throwing")]
    public float throwForce = 10f;
    public float throwUpwardArc = 2f;
    [SerializeField] GameObject landingRingPrefab;

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
            if (target != null) target.Interact(this);
        }
        if (keyboard.fKey.wasPressedThisFrame) UseSelectedItem();
        if (keyboard.qKey.wasPressedThisFrame) ThrowSelectedItem();
        DrainBattery();
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

        thrown.AddComponent<ThrownItem>().Init(landingRingPrefab);

        manager.RemoveItem(manager.SelectedIndex);
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

        brickHeldModel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        brickHeldModel.name = "HeldBrick";
        brickHeldModel.transform.SetParent(heldItemAnchor, false);
        brickHeldModel.transform.localScale = Vector3.one * 0.22f;
        Destroy(brickHeldModel.GetComponent<Collider>());
        DisableShadows(brickHeldModel);
        brickHeldModel.SetActive(false);

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

    Interactable AimedInteractable()
    {
        if (eyes == null) return null;
        int count = Physics.SphereCastNonAlloc(eyes.transform.position, 0.08f,
            eyes.transform.forward, reach, interactionDistance, ~0, QueryTriggerInteraction.Collide);
        Interactable nearest = null;
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            var candidate = reach[i].collider.GetComponentInParent<Interactable>();
            if (candidate == null || reach[i].distance >= nearestDistance) continue;
            nearest = candidate;
            nearestDistance = reach[i].distance;
        }
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
